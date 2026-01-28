using ILICheck.Web.Tools;
using ILICheck.Web.XtfLog;
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
using static ILICheck.Web.ValidatorHelper;

namespace ILICheck.Web
{
    /// <summary>
    /// Validates an INTERLIS <see cref="TransferFile"/> at the given <see cref="HomeDirectory"/>.
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

        /// <inheritdoc/>
        public virtual string HomeDirectory => fileProvider.HomeDirectory.FullName;

        /// <inheritdoc/>
        public virtual string TransferFile { get; private set; }

        /// <inheritdoc/>
        public virtual string GpkgModelNames { get; private set; }

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
            ArgumentNullException.ThrowIfNull(transferFile);
            if (string.IsNullOrWhiteSpace(transferFile)) throw new ArgumentException("Transfer file name cannot be empty.", nameof(transferFile));
            if (!fileProvider.Exists(transferFile)) throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Transfer file with the specified name <{0}> not found in <{1}>.", transferFile, fileProvider.HomeDirectory));

            // Set the fully qualified path to the transfer file.
            TransferFile = transferFile;

            // Unzip compressed file
            if (Path.GetExtension(TransferFile) == ".zip")
            {
                await UnzipCompressedFileAsync().ConfigureAwait(false);
            }

            // Read model names from GeoPackage
            if (Path.GetExtension(TransferFile) == ".gpkg")
            {
                GpkgModelNames = await ReadGpkgModelNamesAsync().ConfigureAwait(false);
            }

            // Additional xml validation for supported files
            var supportedExtensions = new[] { ".xml", ".xtf" };
            if (supportedExtensions.Contains(Path.GetExtension(TransferFile), StringComparer.OrdinalIgnoreCase))
            {
                await ValidateXmlAsync().ConfigureAwait(false);
            }

            try
            {
                // Execute validation with ilivalidator
                await ValidateAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Clean up user uploaded/uncompressed files
                await CleanUploadDirectoryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously unzips the content of the compressed <see cref="TransferFile"/> into the same directory.
        /// If succeeded, the original compressed transfer file gets deleted and <see cref="TransferFile"/> path is set to the new file.
        /// </summary>
        /// <exception cref="NotSupportedException">If <see cref="TransferFile"/> file extension is not supported.</exception>
        internal async Task UnzipCompressedFileAsync()
        {
            if (!string.Equals(Path.GetExtension(TransferFile), ".zip", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Only .zip files are supported.");

            logger.LogInformation("Unzipping compressed file <{TransferFile}>", TransferFile);

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(Path.Combine(fileProvider.HomeDirectory.FullName, TransferFile));

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

                // Set new transfer file
                TransferFile = fileProvider.GetFiles().Single(file => Path.GetExtension(file).Equals(transferFileExtension, StringComparison.OrdinalIgnoreCase));
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously validates the xml structure of <see cref="fileProvider"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <see cref="TransferFile"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <see cref="TransferFile"/> is <c>string.Empty</c>.</exception>
        /// <exception cref="FileNotFoundException">If <see cref="TransferFile"/> is not found.</exception>
        public async Task ValidateXmlAsync()
        {
            ArgumentNullException.ThrowIfNull(TransferFile);
            if (string.IsNullOrWhiteSpace(TransferFile)) throw new ArgumentException("Transfer file name cannot be empty.", nameof(TransferFile));

            logger.LogInformation("Validating xml structure for transfer file <{TransferFile}>", TransferFile);

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                Async = true,
            };

            using var fileStream = fileProvider.OpenText(TransferFile);
            using var reader = XmlReader.Create(fileStream, settings);

            try
            {
                // Validate XML file.
                while (await reader.ReadAsync().ConfigureAwait(false)) { }
            }
            catch (XmlException ex)
            {
                throw new InvalidXmlException(
                    string.Format(CultureInfo.InvariantCulture, "Cannot parse transfer file <{0}>: {1}", TransferFile, ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Asynchronously gets the model names from a GeoPackage SQLite database.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <see cref="TransferFile"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <see cref="TransferFile"/> is <c>string.Empty</c>.</exception>
        /// <exception cref="FileNotFoundException">If <see cref="TransferFile"/> is not found.</exception>
        internal async Task<string> ReadGpkgModelNamesAsync()
        {
            ArgumentNullException.ThrowIfNull(TransferFile);
            if (string.IsNullOrWhiteSpace(TransferFile)) throw new ArgumentException("Transfer file name cannot be empty.", nameof(TransferFile));

            logger.LogInformation("Reading model names from GeoPackage <{TransferFile}>", TransferFile);

            try
            {
                var connectionString = $"Data Source={Path.Combine(fileProvider.HomeDirectory.FullName, TransferFile)}";
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
        /// Asynchronously validates the <see cref="TransferFile"/> with ilivalidator/ili2gpkg.
        /// </summary>
        internal async Task ValidateAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Validating transfer file <{TransferFile}> with ilivalidator/ili2gpkg", TransferFile);

            var homeDirectory = fileProvider.HomeDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var transferFileNameWithoutExtension = Path.GetFileNameWithoutExtension(TransferFile);
            var logPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.log");
            var xtfLogPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.xtf");
            var transferFilePath = Path.Combine(homeDirectory, TransferFile);
            var xmlCatalogFiles = fileProvider.GetFiles()
                .Where(file => Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase) && !file.Equals(TransferFile, StringComparison.OrdinalIgnoreCase))
                .Select(file => Path.Combine(homeDirectory, file))
                .ToList();

            var request = new ValidationRequest
            {
                TransferFileName = TransferFile,
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
        internal async Task CleanUploadDirectoryAsync()
        {
            var tasks = fileProvider.GetFiles()
                .GetFilesToDelete(configuration, TransferFile)
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

            var reader = command.ExecuteReader();
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
