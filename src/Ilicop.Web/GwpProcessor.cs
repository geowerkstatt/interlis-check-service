using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;
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
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    public async Task Run(Guid jobId, string transferFile, Profile profile, CancellationToken cancellationToken)
    {
        if (configDir == null || !Directory.Exists(Path.Combine(configDir.FullName, profile.Id)))
        {
            logger.LogInformation("No configuration directory found for profile <{ProfileId}>. Skipping GWP processing for job <{JobId}>.", profile.Id, jobId);
            return;
        }

        fileProvider.Initialize(jobId);

        if (TryCopyTemplateGpkg(profile, out var dataGpkgFilePath))
        {
            await ImportTransferFileToGpkg(fileProvider, dataGpkgFilePath, transferFile, profile, cancellationToken);
            await ImportLogToGpkg(fileProvider, dataGpkgFilePath, profile, cancellationToken);
            TryCopyQgisServiceFile(fileProvider, profile);
        }
        else
        {
            logger.LogWarning("No data GeoPackage file found at <{GpkgFilePath}> for profile <{ProfileId}>. Skipping GWP GeoPackage creation for job <{JobId}>.", dataGpkgFilePath, profile.Id, jobId);
        }

        CreateZip(jobId, profile);
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

    private bool TryCopyQgisServiceFile(IFileProvider fileProvider, Profile profile)
    {
        var serviceFile = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.QgisProjectFileName);
        if (!File.Exists(serviceFile)) return false;

        try
        {
            logger.LogInformation("Copying QGIS project file for profile <{ProfileId}>.", profile.Id);
            File.Copy(serviceFile, Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.QgisProjectFileName), true);
        }
        catch (SystemException ex)
        {
            logger.LogError(ex, "Failed to copy QGIS project file for profile <{ProfileId}>.", profile.Id);
            return false;
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
                archive.CreateEntryFromFile(fileToZip.FilePath, fileToZip.FileName, CompressionLevel.Optimal);
                logger.LogTrace("Added file <{FileName}> to ZIP for job <{JobId}>", fileToZip.FileName, jobId);
            }
        }

        logger.LogInformation("Successfully created ZIP for job <{JobId}>.", jobId);
    }

    private List<(string FilePath, string FileName)> GetFilesToZip(Profile profile)
    {
        var filesToZip = new List<(string Path, string Name)>();
        filesToZip.AddRange(GetLogFilesToZip(fileProvider));
        filesToZip.AddRange(GetAdditionalFilesToZip(fileProvider, profile));

        // Add GeoPackage if exists
        var gpkgFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.DataGpkgFileName);
        if (File.Exists(gpkgFilePath))
        {
            filesToZip.Add((gpkgFilePath, gwpProcessorOptions.DataGpkgFileName));
        }

        return filesToZip;
    }

    private IEnumerable<(string FilePath, string FileName)> GetLogFilesToZip(IFileProvider fileProvider)
    {
        return fileProvider.GetFiles()
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_log", true, CultureInfo.InvariantCulture))
            .Select(f => (FilePath: Path.Combine(fileProvider.HomeDirectory.FullName, f), FileName: $"log{Path.GetExtension(f)}"));
    }

    private IEnumerable<(string FilePath, string FileName)> GetAdditionalFilesToZip(IFileProvider fileProvider, Profile profile)
    {
        var additionalFilesDirPath = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.AdditionalFilesFolderName);

        if (!Directory.Exists(additionalFilesDirPath))
            return Enumerable.Empty<(string, string)>();

        return Directory.GetFiles(additionalFilesDirPath)
            .Select(f => (FilePath: f, FileName: Path.GetFileName(f)));
    }
}
