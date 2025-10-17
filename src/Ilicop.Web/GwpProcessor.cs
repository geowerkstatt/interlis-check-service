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

        configDir = new DirectoryInfo(configuration.GetValue<string>(configDirectoryEnvironmentKey));
    }

    /// <inheritdoc />
    public void Run(Guid jobId, Profile profile)
    {
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

        var aprxFilePath = GetTemplateFilePath(profile, "*.aprx");
        var qgzFilePath = GetTemplateFilePath(profile, "*.qgz");

        if (aprxFilePath != null) filesToZip.Add((aprxFilePath, Path.GetFileName(aprxFilePath)));
        if (qgzFilePath != null) filesToZip.Add((qgzFilePath, Path.GetFileName(qgzFilePath)));

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

    /// <summary>
    /// Gets the full file path of the file matching the <paramref name="searchPattern"/> in the template folder for the specified <paramref name="profile"/>.
    /// The <paramref name="searchPattern"/> must not match more than one file; otherwise an exception is thrown.
    /// </summary>
    /// <param name="profile">The profile for which to get the template file path.</param>
    /// <param name="searchPattern">The search pattern to match the template file. "?" and "*" can be used as wildcards. Regex is not supported.</param>
    /// <returns>The full file path of the matching file; otherwise, <c>null</c> if no file matches the <paramref name="searchPattern"/>.</returns>
    /// <exception cref="InvalidOperationException">If more than one file matches the <paramref name="searchPattern"/>.</exception>
    private string GetTemplateFilePath(Profile profile, string searchPattern)
    {
        var profileTemplatesPath = Path.Combine(configDir.FullName, profile.Id);

        if (Directory.Exists(profileTemplatesPath))
        {
            var matchingFiles = Directory
                .GetFiles(profileTemplatesPath, searchPattern, SearchOption.TopDirectoryOnly);

            if (matchingFiles.Length > 1)
            {
                throw new InvalidOperationException($"More than one file matches the search pattern '{searchPattern}' for profile '{profile.Id}'.");
            }

            return matchingFiles.SingleOrDefault();
        }

        return null;
    }
}
