using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Geowerkstatt.Ilicop.Web;

/// <summary>
/// Processor for all GWP related tasks.
/// </summary>
public class GwpProcessor : IProcessor
{
    private const string AdditionalFilesFolderName = "AdditionalFiles";

    private readonly IConfiguration configuration;
    private readonly IFileProvider fileProvider;
    private readonly DirectoryInfo configDir;
    private readonly ILogger<GwpProcessor> logger;

    public GwpProcessor(IConfiguration configuration, string configDirectoryEnvironmentKey, IFileProvider fileProvider, ILogger<GwpProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(configDirectoryEnvironmentKey, nameof(configDirectoryEnvironmentKey));

        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configDirectoryEnvironmentValue = configuration.GetValue<string>(configDirectoryEnvironmentKey);
        if (configDirectoryEnvironmentValue != null)
            configDir = new DirectoryInfo(configDirectoryEnvironmentValue);
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

        fileProvider.Initialize(jobId);
        var homeDirectory = fileProvider.HomeDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var filesToZip = new List<(string Path, string Name)>();

        foreach (var logType in Enum.GetValues<LogType>())
        {
            try
            {
                var logFileName = fileProvider.GetLogFile(logType);
                var logFileFullPath = Path.Combine(homeDirectory, logFileName);
                filesToZip.Add((logFileFullPath, $"log.{logType}"));
            }
            catch
            {
                logger.LogTrace("Log file for log type <{LogType}> not found for job <{JobId}>.", logType, jobId);
            }
        }

        var additionalFilesDirPath = Path.Combine(configDir.FullName, profile.Id, AdditionalFilesFolderName);
        if (Directory.Exists(additionalFilesDirPath))
        {
            var additionalFiles = Directory.GetFiles(additionalFilesDirPath);
            foreach (var additionalFile in additionalFiles)
            {
                var fileName = Path.GetFileName(additionalFile);
                filesToZip.Add((additionalFile, fileName));
            }
        }

        var zipFileStream = fileProvider.CreateFile("gwp_results_log.zip");
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
}
