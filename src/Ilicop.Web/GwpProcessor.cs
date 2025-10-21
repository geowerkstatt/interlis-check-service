using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;
using Microsoft.Extensions.Configuration;
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
    public async Task Run(Guid jobId, string transferFile, Profile profile, CancellationToken cancellationToken)
    {
        if (configDir == null || !Directory.Exists(Path.Combine(configDir.FullName, profile.Id)))
        {
            logger.LogInformation("No configuration directory found for profile <{ProfileId}>. Skipping GWP processing for job <{JobId}>.", profile.Id, jobId);
            return;
        }

        await CreateGpkg(jobId, transferFile, profile, cancellationToken);
        CreateZip(jobId, profile);
    }

    private async Task CreateGpkg(Guid jobId, string transferFile, Profile profile, CancellationToken cancellationToken)
    {
        var dataGpkgFilePath = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.DataGpkgFileName);

        if (!File.Exists(dataGpkgFilePath))
        {
            logger.LogWarning("No data GeoPackage file found at <{GpkgFilePath}> for profile <{ProfileId}>. Skipping GWP GeoPackage creation for job <{JobId}>.", dataGpkgFilePath, profile.Id, jobId);
            return;
        }

        fileProvider.Initialize(jobId);
        var destGpkgFilePath = Path.Combine(fileProvider.HomeDirectory.FullName, gwpProcessorOptions.DataGpkgFileName);

        using (var destGpkgFileStream = fileProvider.CreateFile(destGpkgFilePath))
        using (var sourceGpkgFileStream = File.OpenRead(dataGpkgFilePath))
        {
            sourceGpkgFileStream.CopyTo(destGpkgFileStream);
        }

        var transferFileImportExitCode = await ImportTransferFileToGpkg(fileProvider, destGpkgFilePath, transferFile, profile, cancellationToken);
        var logFileImportExitCode = await ImportLogToGpkg(fileProvider, destGpkgFilePath, profile, cancellationToken);

        if (transferFileImportExitCode + logFileImportExitCode != 0)
        {
            logger.LogInformation("Data could not be imported into GeoPackage for job <{JobId}>.", jobId);
            File.Delete(destGpkgFilePath);
        }
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

        var filesToZip = GetFilesToZip(jobId, profile);

        var zipFileStream = fileProvider.CreateFile(gwpProcessorOptions.ZipFileName);
        using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
        {
            foreach (var fileToZip in filesToZip)
            {
                archive.CreateEntryFromFile(fileToZip.Path, fileToZip.Name, CompressionLevel.Optimal);
                logger.LogTrace("Added file <{FileName}> to ZIP for job <{JobId}>", fileToZip.Name, jobId);
            }
        }

        logger.LogInformation("Successfully created ZIP for job <{JobId}>.", jobId);
    }

    private List<(string Path, string Name)> GetFilesToZip(Guid jobId, Profile profile)
    {
        fileProvider.Initialize(jobId);

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

    private IEnumerable<(string Path, string Name)> GetLogFilesToZip(IFileProvider fileProvider)
    {
        return fileProvider.GetFiles()
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_log", true, CultureInfo.InvariantCulture))
            .Select(f => (Path: Path.Combine(fileProvider.HomeDirectory.FullName, f), Name: $"log{Path.GetExtension(f)}"));
    }

    private IEnumerable<(string Path, string Name)> GetAdditionalFilesToZip(IFileProvider fileProvider, Profile profile)
    {
        var additionalFilesDirPath = Path.Combine(configDir.FullName, profile.Id, gwpProcessorOptions.AdditionalFilesFolderName);

        if (!Directory.Exists(additionalFilesDirPath))
            return Enumerable.Empty<(string Path, string Name)>();

        return Directory.GetFiles(additionalFilesDirPath)
            .Select(f => (Path: f, Name: Path.GetFileName(f)));
    }
}
