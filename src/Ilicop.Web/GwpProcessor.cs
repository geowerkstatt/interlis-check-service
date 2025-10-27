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
            var importTransferFileExitCode = await ImportTransferFileToGpkg(fileProvider, dataGpkgFilePath, transferFile.FileName, profile, cancellationToken);
            var importLogTransferFileExitCode = await ImportLogToGpkg(fileProvider, dataGpkgFilePath, profile, cancellationToken);

            if (importLogTransferFileExitCode == 0 && importTransferFileExitCode == 0)
            {
                if (IsTranslationNeeded(dataGpkgFilePath))
                    await CreateTranslatedTransferFile(dataGpkgFilePath, transferFile, profile, cancellationToken);
            }
            else
            {
                File.Delete(dataGpkgFilePath);
                logger.LogWarning("Importing transfer file or log file to GeoPackage failed for profile <{ProfileId}>. Deleting GeoPackage again for job <{JobId}>.", profile.Id, jobId);
            }
        }
        else
        {
            logger.LogWarning("No data GeoPackage file found at <{GpkgFilePath}> for profile <{ProfileId}>. Skipping GWP GeoPackage creation for job <{JobId}>.", dataGpkgFilePath, profile.Id, jobId);
        }

        CreateZip(jobId, transferFile, profile);
    }

    private async Task CreateTranslatedTransferFile(string gpkgPath, NamedFile transferFile, Profile profile, CancellationToken cancellationToken)
    {
        var translatedTransferFile = GetTranslatedTransferFile(transferFile);

        var exportRequest = new ExportRequest
        {
            FileName = translatedTransferFile.FileName,
            FilePath = translatedTransferFile.FilePath,
            Profile = profile,
            DbFilePath = gpkgPath,
            Dataset = "Data",
        };

        await ilitoolsExecutor.ExportFromGpkgAsync(exportRequest, cancellationToken);
    }

    private bool IsTranslationNeeded(string gpkgPath)
    {
        var models = GetColumnFromSqliteTable(gpkgPath, "T_ILI2DB_MODEL", "modelName").Select(m => m.ToString());
        var topics = GetColumnFromSqliteTable(gpkgPath, "T_ILI2DB_BASKET", "topic").Select(m => m.ToString());

        return GetBasketTopicsNotInModels(topics, models).Any();
    }

    internal IEnumerable<string> GetBasketTopicsNotInModels(IEnumerable<string> topics, IEnumerable<string> models)
    {
        var splitModels = models.SelectMany(r => r.Replace("{", "").Replace("}", "").Split(' ')).Distinct().ToList();
        var importedBasketsModel = topics.Select(r => r.Split('.')[0]).Distinct().ToList();

        return importedBasketsModel.Except(splitModels);
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

    private void CreateZip(Guid jobId, NamedFile transferFile, Profile profile)
    {
        logger.LogInformation("Creating ZIP for job <{JobId}>.", jobId);

        var filesToZip = GetFilesToZip(transferFile, profile);

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

    private List<NamedFile> GetFilesToZip(NamedFile transferFile, Profile profile)
    {
        var filesToZip = new List<NamedFile>();
        filesToZip.AddRange(GetLogFilesToZip(fileProvider));
        filesToZip.AddRange(GetAdditionalFilesToZip(fileProvider, profile));

        // Add GeoPackage if exists
        var gpkgFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.DataGpkgFileName);
        if (File.Exists(gpkgFilePath))
            filesToZip.Add(new NamedFile(gpkgFilePath, gwpProcessorOptions.DataGpkgFileName));

        // Add translated transfer file if exists
        var translatedTransferFile = GetTranslatedTransferFile(transferFile);
        if (File.Exists(translatedTransferFile.FilePath))
            filesToZip.Add(translatedTransferFile);

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

        SqliteConnection.ClearAllPools();
    }

    private NamedFile GetTranslatedTransferFile(NamedFile transferFile)
    {
        var suffix = "_translated.xtf";
        var fileName = $"{Path.GetFileNameWithoutExtension(transferFile.FileName)}{suffix}";
        var displayName = $"{Path.GetFileNameWithoutExtension(transferFile.DisplayName)}{suffix}";
        var path = Path.Combine(fileProvider.HomeDirectory.FullName, fileName);
        return new NamedFile(path, displayName);
    }
}
