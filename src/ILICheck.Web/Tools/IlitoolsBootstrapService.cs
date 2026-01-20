using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ILICheck.Web.Tools
{
    /// <summary>
    /// Service responsible for bootstrapping and managing ilitools.
    /// </summary>
    public partial class IlitoolsBootstrapService : IHostedService
    {
        private readonly ILogger<IlitoolsBootstrapService> logger;
        private readonly IConfiguration configuration;
        private readonly HttpClient httpClient;
        private readonly IlitoolsEnvironment ilitoolsEnvironment;

        public IlitoolsBootstrapService(ILogger<IlitoolsBootstrapService> logger, IConfiguration configuration, HttpClient httpClient, IlitoolsEnvironment ilitoolsEnvironment)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.httpClient = httpClient;
            this.ilitoolsEnvironment = ilitoolsEnvironment;
        }

        /// <summary>
        /// Starts the service and bootstraps the ilitools.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting ilitools bootstrap service");

            try
            {
                // Set environment variables for ilitools
                Environment.SetEnvironmentVariable("ILI_CACHE", ilitoolsEnvironment.CacheDir);
                logger.LogDebug("Set ILI_CACHE environment variable to: {IlitoolsCacheDir}", ilitoolsEnvironment.CacheDir);

                ilitoolsEnvironment.TraceEnabled = configuration.GetValue<bool>("ILIVALIDATOR_ENABLE_TRACE");

                // Bootstrap ilivalidator
                await InitializeIlivalidatorAsync(cancellationToken);

                // Bootstrap ili2gpkg if needed
                if (ilitoolsEnvironment.EnableGpkgValidation)
                {
                    await InitializeIli2GpkgAsync(cancellationToken);
                }
                else
                {
                    logger.LogInformation("GPKG validation is disabled, skipping ili2gpkg bootstrap.");
                }

                logger.LogInformation("Ilitools successfully initialized!");
                logger.LogInformation(ilitoolsEnvironment.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to bootstrap ilitools.");
                throw new InvalidOperationException("Ilitools initialization failed.", ex);
            }
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task InitializeIlivalidatorAsync(CancellationToken cancellationToken)
        {
            var version = configuration.GetValue<string>("ILIVALIDATOR_VERSION");
            if (string.IsNullOrEmpty(version))
            {
                logger.LogInformation("ILIVALIDATOR_VERSION not specified, fetching latest version.");

                version = await GetLatestIlivalidatorVersionAsync(cancellationToken);
                if (string.IsNullOrEmpty(version))
                {
                    logger.LogWarning("Failed to fetch the latest ilivalidator version. Falling back to the latest installed version.");
                    version = GetLatestInstalledIlitoolVersion("ilivalidator");
                }
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new InvalidOperationException($"Unable to determine ilivalidator version.");
            }

            var installDir = await DownloadAndConfigureIlitoolAsync("ilivalidator", version, cancellationToken);
            var jarPath = Path.Combine(installDir, $"ilivalidator-{version}.jar");

            ilitoolsEnvironment.IlivalidatorVersion = version;
            ilitoolsEnvironment.IlivalidatorPath = jarPath;

            Environment.SetEnvironmentVariable("ILIVALIDATOR_VERSION", version);

            logger.LogInformation("ilivalidator version {Version} initialized successfully at {Path}", version, jarPath);
        }

        private async Task InitializeIli2GpkgAsync(CancellationToken cancellationToken)
        {
            var version = configuration.GetValue<string>("ILI2GPKG_VERSION");
            if (string.IsNullOrEmpty(version))
            {
                logger.LogInformation("ILI2GPKG_VERSION not specified, fetching latest version.");

                version = await GetLatestIli2GpkgVersionAsync(cancellationToken);
                if (string.IsNullOrEmpty(version))
                {
                    logger.LogWarning("Failed to fetch the latest ili2gpkg version. Falling back to the latest installed version.");
                    version = GetLatestInstalledIlitoolVersion("ili2gpkg");
                }
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new InvalidOperationException($"Unable to determine ili2gpkg version.");
            }

            var installDir = await DownloadAndConfigureIlitoolAsync("ili2gpkg", version, cancellationToken);
            var jarPath = Path.Combine(installDir, $"ili2gpkg-{version}.jar");

            ilitoolsEnvironment.Ili2GpkgVersion = version;
            ilitoolsEnvironment.Ili2GpkgPath = jarPath;

            Environment.SetEnvironmentVariable("ILI2GPKG_VERSION", version);

            logger.LogInformation("ili2gpkg version {Version} initialized successfully at {Path}", version, jarPath);
        }

        /// <summary>
        /// Downloads and configures the specified ilitool.
        /// </summary>
        /// <returns>The path to the installation directory of the the given ilitool (e.g. /ilitools/ilivalidator/1.14.9).</returns>
        private async Task<string> DownloadAndConfigureIlitoolAsync(string ilitool, string version, CancellationToken cancellationToken)
        {
            // Exit if the tool is already installed and valid
            var installDir = Path.Combine(ilitoolsEnvironment.InstallationDir, ilitool, version);
            var jarPath = Path.Combine(installDir, $"{ilitool}-{version}.jar");
            if (Directory.Exists(installDir) && File.Exists(jarPath))
            {
                logger.LogInformation("{Ilitool}-{Version} is already installed at {Path}. Skipping download and configuration.", ilitool, version, jarPath);
                return installDir;
            }

            logger.LogInformation("Download and configure {Ilitool}-{Version}...", ilitool, version);

            try
            {
                // Ensure the ilitools home directory exists
                if (!Directory.Exists(ilitoolsEnvironment.InstallationDir))
                {
                    Directory.CreateDirectory(ilitoolsEnvironment.InstallationDir);
                    logger.LogDebug("Created ilitools home directory: {IlitoolsHomeDir}", ilitoolsEnvironment.InstallationDir);
                }

                var downloadUrl = new UriBuilder($"https://downloads.interlis.ch/{ilitool}/{ilitool}-{version}.zip");
                var tempFilePath = Path.GetTempFileName();

                // Download the zip file
                logger.LogDebug("Downloading {Ilitool} from {DownloadUrl}", ilitool, downloadUrl);
                using var response = await httpClient.GetAsync(downloadUrl.Uri.AbsoluteUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }

                logger.LogDebug(
                    "Downloaded {Ilitool} zip file to {TempFilePath} ({Size} bytes)",
                    ilitool,
                    tempFilePath,
                    new FileInfo(tempFilePath).Length);

                // Create install directory
                Directory.CreateDirectory(installDir);
                logger.LogDebug("Created install directory: {InstallDir}", installDir);

                // Extract the zip file
                ZipFile.ExtractToDirectory(tempFilePath, installDir, overwriteFiles: true);
                logger.LogDebug("Extracted {Ilitool} to {InstallDir}", ilitool, installDir);

                // Verify the JAR file exists
                if (!File.Exists(jarPath))
                {
                    throw new FileNotFoundException($"Expected JAR file not found after extraction: {jarPath}");
                }

                return installDir;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not install {Ilitool}-{Version}.", ilitool, version);
                throw;
            }
        }

        private async Task<string> GetLatestIlivalidatorVersionAsync(CancellationToken ct)
            => await GetLatestToolVersionAsync("https://www.interlis.ch/downloads/ilivalidator", @"(?<=ilivalidator-)\d+\.\d+\.\d+", "ilivalidator", ct);

        private async Task<string> GetLatestIli2GpkgVersionAsync(CancellationToken ct)
            => await GetLatestToolVersionAsync("https://www.interlis.ch/downloads/ili2db", @"(?<=ili2gpkg-)\d+\.\d+\.\d+", "ili2gpkg", ct);

        private async Task<string> GetLatestToolVersionAsync(string url, string pattern, string tool, CancellationToken ct)
        {
            try
            {
                var html = await httpClient.GetStringAsync(url, ct);
                var match = Regex.Match(html, pattern);
                return match.Success ? match.Value : null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch latest {Tool} version.", tool);
                return null;
            }
        }

        /// <summary>
        /// Gets the latest installed version of the specified ilitool.
        /// </summary>
        internal string GetLatestInstalledIlitoolVersion(string ilitool)
        {
            try
            {
                var toolDir = Path.Combine(ilitoolsEnvironment.InstallationDir, ilitool);
                if (!Directory.Exists(toolDir))
                {
                    logger.LogDebug("Tool directory does not exist: {ToolDir}", toolDir);
                    return null;
                }

                var versions = Directory.GetDirectories(toolDir)
                    .Select(Path.GetFileName)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .OrderBy(v => new Version(v))
                    .ToList();

                if (versions.Count == 0)
                {
                    logger.LogDebug("No valid installed versions found for {Ilitool}.", ilitool);
                    return null;
                }

                var latestVersion = versions.LastOrDefault() ?? null;
                logger.LogDebug("Latest installed version for {Ilitool}: {Version}", ilitool, latestVersion);
                return latestVersion;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get latest installed version for {Ilitool}.", ilitool);
                return null;
            }
        }
    }
}
