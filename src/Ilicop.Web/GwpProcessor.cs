using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web;

/// <summary>
/// Processor for all GWP related tasks.
/// </summary>
public class GwpProcessor : IProcessor
{
    private readonly IFileProvider fileProvider;
    private readonly DirectoryInfo configDir;
    private readonly ILogger<GwpProcessor> logger;
    private readonly GwpProcessorOptions gwpProcessorOptions;
    private readonly IlitoolsExecutor ilitoolsExecutor;

    public GwpProcessor(
        IOptions<GwpProcessorOptions> gwpProcessorOptions,
        IFileProvider fileProvider,
        IlitoolsExecutor ilitoolsExecutor,
        ILogger<GwpProcessor> logger)
    {
        this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.gwpProcessorOptions = gwpProcessorOptions?.Value ?? throw new ArgumentNullException(nameof(gwpProcessorOptions));
        this.ilitoolsExecutor = ilitoolsExecutor ?? throw new ArgumentNullException(nameof(ilitoolsExecutor));

        if (this.gwpProcessorOptions.ConfigDir != null)
            this.configDir = new DirectoryInfo(this.gwpProcessorOptions.ConfigDir);
    }

    /// <inheritdoc />
    public async Task Run(Guid jobId, NamedFile transferFile, Profile profile, CancellationToken cancellationToken)
    {
        if (configDir == null || !Directory.Exists(Path.Combine(configDir.FullName, profile.Id)))
        {
            logger.LogInformation("No configuration directory found for profile <{ProfileId}>. Skipping GWP processing for job <{JobId}>.", profile.Id, jobId);
            return;
        }

        fileProvider.Initialize(jobId);

        if (TryCopyTemplateGpkg(profile, out var dataGpkgFilePath))
        {
            await ImportTransferFileToGpkg(fileProvider, dataGpkgFilePath, transferFile.FileName, profile, cancellationToken);
            await ImportLogToGpkg(fileProvider, dataGpkgFilePath, profile, cancellationToken);

            if (IsTranslationNeeded(dataGpkgFilePath))
                await CreateTranslatedTransferFile(dataGpkgFilePath, profile, cancellationToken);
        }
        else
        {
            logger.LogWarning("No data GeoPackage file found at <{GpkgFilePath}> for profile <{ProfileId}>. Skipping GWP GeoPackage creation for job <{JobId}>.", dataGpkgFilePath, profile.Id, jobId);
        }

        CreateZip(jobId, profile);
    }

    private async Task CreateTranslatedTransferFile(string gpkgPath, Profile profile, CancellationToken cancellationToken)
    {
        var outputFileName = "translated.xtf";
        var outputFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, outputFileName);

        var exportRequest = new ExportRequest
        {
            FileName = outputFileName,
            FilePath = outputFilePath,
            Profile = profile,
            DbFilePath = gpkgPath,
            Dataset = "Data",
        };

        await ilitoolsExecutor.ExportFromGpkgAsync(exportRequest, cancellationToken);
    }

    private bool IsTranslationNeeded(string gpkgPath)
    {
        var modelNames = GetColumnFromSqliteTable(gpkgPath, "T_ILI2DB_MODEL", "modelName").Select(r => r.ToString()).ToList();
        var topics = GetColumnFromSqliteTable(gpkgPath, "T_ILI2DB_BASKET", "topic").Select(r => r.ToString()).ToList();

        SqliteConnection.ClearAllPools(); // Required because otherwise the database file remains locked

        return topics.Select(n => n.Split('.')[0]).Any(x => modelNames.All(n => !n.Contains(x)));
    }

    private bool TryCopyTemplateGpkg(Profile profile, out string dataGpkgFilePath)
    {
        var templateGpkgFilePath = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.DataGpkgFileName);

        if (!File.Exists(templateGpkgFilePath))
        {
            dataGpkgFilePath = null;
            return false;
        }

        dataGpkgFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.DataGpkgFileName);

        using (var destGpkgFileStream = fileProvider.CreateFile(dataGpkgFilePath))
        using (var sourceGpkgFileStream = File.OpenRead(templateGpkgFilePath))
        {
            sourceGpkgFileStream.CopyTo(destGpkgFileStream);
        }

        return true;
    }

    private async Task<int> ImportLogToGpkg(IFileProvider fileProvider, string gpkgFilePath, Profile profile, CancellationToken cancellationToken)
    {
        var logFileName = fileProvider.GetFiles().FirstOrDefault(f => f.EndsWith("_log.xtf", StringComparison.InvariantCultureIgnoreCase));
        var logFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, logFileName);

        var logFileImportRequest = new ImportRequest
        {
            FilePath = logFilePath,
            FileName = logFileName,
            DbFilePath = gpkgFilePath,
            Profile = profile,
            Dataset = "Logs",
        };

        return await ilitoolsExecutor.ImportToGpkgAsync(logFileImportRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ImportTransferFileToGpkg(IFileProvider fileProvider, string gpkgFilePath, string transferFile, Profile profile, CancellationToken cancellationToken)
    {
        var transferFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, transferFile);

        var transferFileImportRequest = new ImportRequest
        {
            FilePath = transferFilePath,
            FileName = transferFile,
            DbFilePath = gpkgFilePath,
            Profile = profile,
            Dataset = "Data",
        };

        return await ilitoolsExecutor.ImportToGpkgAsync(transferFileImportRequest, cancellationToken).ConfigureAwait(false);
    }

    private void CreateZip(Guid jobId, Profile profile)
    {
        logger.LogInformation("Creating ZIP for job <{JobId}>.", jobId);

        var filesToZip = GetFilesToZip(profile);

        var zipFileStream = fileProvider.CreateFile(gwpProcessorOptions.ZipFileName);
        using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
        {
            foreach (var fileToZip in filesToZip)
            {
                archive.CreateEntryFromFile(fileToZip.FilePath, fileToZip.DisplayName, CompressionLevel.Optimal);
                logger.LogTrace("Added file <{FileName}> to ZIP for job <{JobId}>", fileToZip.DisplayName, jobId);
            }
        }

        logger.LogInformation("Successfully created ZIP for job <{JobId}>.", jobId);
    }

    private List<NamedFile> GetFilesToZip(Profile profile)
    {
        var filesToZip = new List<NamedFile>();
        filesToZip.AddRange(GetLogFilesToZip(fileProvider));
        filesToZip.AddRange(GetAdditionalFilesToZip(fileProvider, profile));

        // Add GeoPackage if exists
        var gpkgFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.DataGpkgFileName);
        if (File.Exists(gpkgFilePath))
            filesToZip.Add(new NamedFile(gpkgFilePath, gwpProcessorOptions.DataGpkgFileName));

        // Add translated transfer file if exists
        var translatedTransferFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, "translated.xtf");
        if (File.Exists(translatedTransferFilePath))
            filesToZip.Add(new NamedFile(translatedTransferFilePath, "translated.xtf"));

        return filesToZip;
    }

    private IEnumerable<NamedFile> GetLogFilesToZip(IFileProvider fileProvider)
    {
        return fileProvider.GetFiles()
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_log", true, CultureInfo.InvariantCulture))
            .Select(f => new NamedFile(Path.Combine(fileProvider.HomeDirectory.FullName, f), $"log{Path.GetExtension(f)}"));
    }

    private IEnumerable<NamedFile> GetAdditionalFilesToZip(IFileProvider fileProvider, Profile profile)
    {
        var additionalFilesDirPath = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.AdditionalFilesFolderName);

        if (!Directory.Exists(additionalFilesDirPath))
            return Enumerable.Empty<NamedFile>();

        return Directory.GetFiles(additionalFilesDirPath)
            .Select(f => new NamedFile(f));
    }

    private IEnumerable<object> GetColumnFromSqliteTable(string dbFilePath, string tableName, string columnName)
    {
        var connectionString = $"Data Source={dbFilePath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
#pragma warning disable CA2100
        command.CommandText = $"SELECT [{columnName}] FROM [{tableName}]";
#pragma warning restore CA2100

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetValue(0);
        }
    }
}
