using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;
using Geowerkstatt.Ilicop.Web.XtfLog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static Geowerkstatt.Ilicop.Web.ValidatorHelper;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Validates an INTERLIS transfer file at the given <see cref="IFileProvider.HomeDirectory"/>.
    /// </summary>
    public class Validator : IValidator
    {
        private readonly ILogger<Validator> logger;
        private readonly IConfiguration configuration;
        private readonly IFileProvider fileProvider;
        private readonly IlitoolsExecutor ilitoolsExecutor;
        private readonly JsonOptions jsonOptions;

        /// <inheritdoc/>
        public virtual Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the extracted model names.
        /// </summary>
        /// <remarks>Only applicable if provided transfer file is a GeoPackage.</remarks>
        private string GpkgModelNames { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator(ILogger<Validator> logger, IConfiguration configuration, IFileProvider fileProvider, IOptions<JsonOptions> jsonOptions, IlitoolsExecutor ilitoolsExecutor)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.fileProvider = fileProvider;
            this.ilitoolsExecutor = ilitoolsExecutor;
            this.jsonOptions = jsonOptions.Value;

            this.fileProvider.Initialize(Id);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string transferFile, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(transferFile);
            if (!fileProvider.Exists(transferFile)) throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Transfer file with the specified name <{0}> not found in <{1}>.", transferFile, fileProvider.HomeDirectory));

            // Unzip compressed file
            if (Path.GetExtension(transferFile) == ".zip")
            {
                transferFile = await UnzipCompressedFileAsync(transferFile).ConfigureAwait(false);
            }

            // Read model names from GeoPackage
            if (Path.GetExtension(transferFile) == ".gpkg")
            {
                GpkgModelNames = await ReadGpkgModelNamesAsync(transferFile).ConfigureAwait(false);
            }

            // Additional xml validation for supported files
            var supportedExtensions = new[] { ".xml", ".xtf" };
            if (supportedExtensions.Contains(Path.GetExtension(transferFile), StringComparer.OrdinalIgnoreCase))
            {
                await ValidateXmlAsync(transferFile).ConfigureAwait(false);
            }

            try
            {
                // Execute validation
                await ValidateAsync(transferFile, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Clean up user uploaded/uncompressed files
                await CleanUploadDirectoryAsync(transferFile).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously unzips the content of the compressed <paramref name="transferFile"/> into the same directory.
        /// If succeeded, the original compressed transfer file gets deleted and <paramref name="transferFile"/> path is set to the new file.
        /// </summary>
        /// <param name="transferFile">The transfer file to unzip.</param>
        /// <returns>The path to the unzipped transfer file.</returns>
        /// <exception cref="NotSupportedException">If <paramref name="transferFile"/> file extension is not supported.</exception>
        private async Task<string> UnzipCompressedFileAsync(string transferFile)
        {
            if (!string.Equals(Path.GetExtension(transferFile), ".zip", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Only .zip files are supported.");

            logger.LogInformation("Unzipping compressed file <{TransferFile}>", transferFile);

            return await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(Path.Combine(fileProvider.HomeDirectory.FullName, transferFile));

                var transferFileExtension = archive.Entries
                    .Select(entry => Path.GetExtension(entry.FullName))
                    .GetTransferFileExtension(configuration);

                foreach (var entry in archive.Entries)
                {
                    var sanitizedFileName = Path.ChangeExtension(
                        Path.GetRandomFileName(),
                        entry.Name.GetSanitizedFileExtension(GetAcceptedFileExtensionsForZipContent(configuration)));

                    entry.ExtractToFile(Path.Combine(fileProvider.HomeDirectory.FullName, sanitizedFileName));
                }

                // Return transfer file from archive
                return fileProvider.GetFiles().Single(file => Path.GetExtension(file).Equals(transferFileExtension, StringComparison.OrdinalIgnoreCase));
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously validates the xml structure of the given <paramref name="transferFile"/>.
        /// </summary>
        /// <param name="transferFile">The transfer file to validate.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="transferFile"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="transferFile"/> is <c>string.Empty</c>.</exception>
        /// <exception cref="FileNotFoundException">If <paramref name="transferFile"/> is not found.</exception>
        internal async Task ValidateXmlAsync(string transferFile)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(transferFile);

            logger.LogInformation("Validating xml structure for transfer file <{TransferFile}>", transferFile);

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                Async = true,
            };

            using var fileStream = fileProvider.OpenText(transferFile);
            using var reader = XmlReader.Create(fileStream, settings);

            try
            {
                // Validate XML file.
                while (await reader.ReadAsync().ConfigureAwait(false)) { }
            }
            catch (XmlException ex)
            {
                throw new InvalidXmlException(
                    string.Format(CultureInfo.InvariantCulture, "Cannot parse transfer file <{0}>: {1}", transferFile, ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Asynchronously gets the model names from a GeoPackage SQLite database.
        /// </summary>
        /// <param name="transferFile">The transfer file to read the model names from.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="transferFile"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="transferFile"/> is <c>string.Empty</c>.</exception>
        /// <exception cref="FileNotFoundException">If <paramref name="transferFile"/> is not found.</exception>
        internal async Task<string> ReadGpkgModelNamesAsync(string transferFile)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(transferFile);

            logger.LogInformation("Reading model names from GeoPackage <{TransferFile}>", transferFile);

            try
            {
                var connectionString = $"Data Source={Path.Combine(fileProvider.HomeDirectory.FullName, transferFile)}";
                return await Task.Run(() =>
                    ReadGpkgModelNameEntries(connectionString)
                    .CleanupGpkgModelNames(configuration)
                    .JoinNonEmpty(";"))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new GeoPackageException(
                    string.Format(CultureInfo.InvariantCulture, "Cannot read model names from the given GeoPackage SQLite database. <{0}>", ex.Message), ex);
            }
        }

        /// <summary>
        /// Asynchronously validates the <paramref name="transferFile"/>> with ilivalidator/ili2gpkg.
        /// </summary>
        private async Task ValidateAsync(string transferFile, CancellationToken cancellationToken)
        {
            logger.LogInformation("Validating transfer file <{TransferFile}> with ilivalidator/ili2gpkg", transferFile);

            var homeDirectory = fileProvider.HomeDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).NormalizeUnixStylePath();
            var transferFileNameWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);
            var logPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.log");
            var xtfLogPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.xtf");
            var transferFilePath = Path.Combine(homeDirectory, transferFile);
            var xmlCatalogFiles = fileProvider.GetFiles()
                .Where(file => Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase) && !file.Equals(transferFile, StringComparison.OrdinalIgnoreCase))
                .Select(file => Path.Combine(homeDirectory, file))
                .ToList();

            var request = new ValidationRequest
            {
                TransferFileName = transferFile,
                TransferFilePath = transferFilePath,
                LogFilePath = logPath,
                XtfLogFilePath = xtfLogPath,
                GpkgModelNames = GpkgModelNames,
                AdditionalCatalogueFilePaths = xmlCatalogFiles,
            };

            var exitCode = await ilitoolsExecutor.ValidateAsync(request, cancellationToken).ConfigureAwait(false);

            await GenerateGeoJsonAsync().ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new ValidationFailedException("The ilivalidator found errors in the file. Validation failed.");
            }

            logger.LogInformation("The ilivalidator found no errors in the file. Validation successful!");
            logger.LogInformation("Validation completed: {Timestamp}", DateTime.Now);
        }

        /// <summary>
        /// Asynchronously cleans up any user uploaded files depending on the setting whether transfer
        /// files should be deleted.
        /// </summary>
        internal async Task CleanUploadDirectoryAsync(string transferFile)
        {
            var tasks = fileProvider.GetFiles()
                .GetFilesToDelete(configuration, transferFile)
                .Select(async file =>
                {
                    logger.LogInformation("Deleting file <{File}>", file);
                    await fileProvider.DeleteFileAsync(file).ConfigureAwait(false);
                });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all available model names from a GeoPackage SQLite database.
        /// </summary>
        /// <param name="connectionString">The string used to open the connection.</param>
        /// <returns>The model names from the specified GeoPackage.</returns>
        private IEnumerable<string> ReadGpkgModelNameEntries(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = new SqliteCommand("SELECT * FROM T_ILI2DB_MODEL", connection);

            using var reader = command.ExecuteReader();
            while (reader.Read()) yield return reader["modelName"].ToString();
        }

        /// <summary>
        /// Asynchronously generates a GeoJSON file from the XTF log file.
        /// </summary>
        private async Task GenerateGeoJsonAsync()
        {
            try
            {
                var xtfLogFile = fileProvider.GetLogFile(LogType.Xtf);
                if (!fileProvider.Exists(xtfLogFile)) return;

                logger.LogInformation("Generating GeoJSON file from XTF log file <{XtfLogFile}>", xtfLogFile);

                using var reader = fileProvider.OpenText(xtfLogFile);
                var logResult = XtfLogParser.Parse(reader);

                var featureCollection = GeoJsonHelper.CreateFeatureCollection(logResult);
                if (featureCollection == null)
                {
                    logger.LogInformation("No or unknown coordinates found in XTF log file <{XtfLogFile}>. Skipping GeoJSON generation.", xtfLogFile);
                    return;
                }

                var geoJsonLogFile = Path.ChangeExtension(xtfLogFile, ".geojson");

                using var geoJsonStream = fileProvider.CreateFile(geoJsonLogFile);
                await JsonSerializer.SerializeAsync(geoJsonStream, featureCollection, jsonOptions.JsonSerializerOptions).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to generate the GeoJSON file from the XTF log file for id <{Id}>.", Id);
            }
        }
    }
}
