using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

    public GwpProcessor(IOptions<GwpProcessorOptions> gwpProcessorOptions, IFileProvider fileProvider, ILogger<GwpProcessor> logger)
    {
        this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.gwpProcessorOptions = gwpProcessorOptions?.Value ?? throw new ArgumentNullException(nameof(gwpProcessorOptions));

        if (this.gwpProcessorOptions.ConfigDir != null)
            this.configDir = new DirectoryInfo(this.gwpProcessorOptions.ConfigDir);
    }

    /// <inheritdoc />
    public void Run(Guid jobId, Profile profile)
    {
        if (configDir == null || !Directory.Exists(Path.Combine(configDir.FullName, profile.Id)))
        {
            logger.LogInformation("No configuration directory found for profile <{ProfileId}>. Skipping GWP processing for job <{JobId}>.", profile.Id, jobId);
            return;
        }

        CreateZip(jobId, profile);
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
