using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Ilitools
{
    /// <summary>
    /// Used for executing ilitools.
    /// </summary>
    public class IlitoolsExecutor
    {
        private readonly ILogger<IlitoolsExecutor> logger;
        private readonly IlitoolsEnvironment ilitoolsEnvironment;
        private readonly IConfiguration configuration;

        public IlitoolsExecutor(ILogger<IlitoolsExecutor> logger, IlitoolsEnvironment ilitoolsEnvironment, IConfiguration configuration)
        {
            this.logger = logger;
            this.ilitoolsEnvironment = ilitoolsEnvironment;
            this.configuration = configuration;
        }

        /// <summary>
        /// Validates a transfer file using the appropriate ilitool based on the request.
        /// </summary>
        public async Task<int> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.IsGeoPackage)
            {
                return await ExecuteIli2GpkgAsync(request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await ExecuteIlivalidatorAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Validates a transfer file using the ilivalidator tool.
        /// </summary>
        private async Task<int> ExecuteIlivalidatorAsync(ValidationRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!ilitoolsEnvironment.IsIlivalidatorInitialized) throw new InvalidOperationException("Ilivalidator is not properly initialized.");

            logger.LogInformation("Starting validation of {TransferFile} using ilivalidator.", request.TransferFileName);

            try
            {
                var command = CreateIlivalidatorCommand(request);

                var exitCode = await ExecuteJavaCommandAsync(command, cancellationToken);

                logger.LogInformation(
                    "Validation completed for {TransferFile} with exit code {ExitCode}.",
                    request.TransferFileName,
                    exitCode);

                return exitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute ilivalidator for {TransferFile}.", request.TransferFileName);
                return -1;
            }
        }

        /// <summary>
        /// Validates a transfer file using the ili2gpkg tool.
        /// </summary>
        private async Task<int> ExecuteIli2GpkgAsync(ValidationRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!ilitoolsEnvironment.IsIli2GpkgInitialized) throw new InvalidOperationException("ili2gpkg is not properly initialized.");
            if (request.AdditionalCatalogueFilePaths.Count > 0) throw new InvalidOperationException("Additional catalogue files are not supported for GPKG validation, aborting validation.");

            logger.LogInformation("Starting validation of {TransferFile} using ili2gpkg.", request.TransferFileName);

            try
            {
                var command = CreateIli2GpkgCommand(request);

                var exitCode = await ExecuteJavaCommandAsync(command, cancellationToken);

                logger.LogInformation(
                    "Validation completed for {TransferFile} with exit code {ExitCode}.",
                    request.TransferFileName,
                    exitCode);

                return exitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute ili2gpkg for {TransferFile}.", request.TransferFileName);
                return -1;
            }
        }

        /// <summary>
        /// Creates the command for executing ilivalidator.
        /// </summary>
        internal string CreateIlivalidatorCommand(ValidationRequest request)
        {
            var args = new List<string>
            {
                "-jar",
                $"\"{ilitoolsEnvironment.IlivalidatorPath}\"",
            };

            // Add plugins
            if (Directory.Exists(ilitoolsEnvironment.PluginsDir))
            {
                var jarFiles = Directory.GetFiles(ilitoolsEnvironment.PluginsDir, "*.jar", SearchOption.TopDirectoryOnly);
                if (jarFiles.Length > 0)
                {
                    args.Add($"--plugins \"{ilitoolsEnvironment.PluginsDir}\"");
                    logger.LogDebug("Added plugins directory with {PluginCount} JAR files", jarFiles.Length);
                }
            }

            // Add csv log if enabled
            args.Add($"--csvlog \"{request.CsvLogFilePath}\"");

            args.AddRange(GetCommonIlitoolsArguments(request));

            // Add transfer file path (without specific parameter name)
            args.Add($"\"{request.TransferFilePath}\"");

            // Add additional catalogue files if present
            foreach (var cataloguePath in request.AdditionalCatalogueFilePaths)
            {
                args.Add($"\"{cataloguePath}\"");
            }

            return args.JoinNonEmpty(" ");
        }

        /// <summary>
        /// Creates the command for executing ili2gpkg.
        /// </summary>
        internal string CreateIli2GpkgCommand(ValidationRequest request)
        {
            var args = new List<string>
            {
                "-jar",
                $"\"{ilitoolsEnvironment.Ili2GpkgPath}\"",
                "--validate",
            };

            // Add model names for GPKG files if specified
            if (!string.IsNullOrEmpty(request.GpkgModelNames))
            {
                args.Add($"--models \"{request.GpkgModelNames}\"");
            }

            args.AddRange(GetCommonIlitoolsArguments(request));

            // Add database file parameter
            args.Add($"--dbfile \"{request.TransferFilePath}\"");

            return args.JoinNonEmpty(" ");
        }

        internal IEnumerable<string> GetCommonIlitoolsArguments(ValidationRequest request)
        {
            // Add common logging options
            yield return $"--log \"{request.LogFilePath}\"";
            yield return $"--xtflog \"{request.XtfLogFilePath}\"";
            yield return "--verbose";

            // Add proxy settings
            var proxy = configuration.GetValue<string>("PROXY");
            if (!string.IsNullOrEmpty(proxy))
            {
                Uri uri = null;
                try
                {
                    uri = new Uri(proxy);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse proxy configuration: {Proxy}", proxy);
                }

                if (!string.IsNullOrEmpty(uri?.Host))
                {
                    yield return $"--proxy {uri.Host}";
                }

                if (uri?.Port != -1)
                {
                    yield return $"--proxyPort {uri.Port}";
                }
            }

            // Add trace if enabled
            if (ilitoolsEnvironment.TraceEnabled)
            {
                yield return "--trace";
            }

            // Add model directory
            yield return $"--modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\"";

            // Add profile
            yield return $"--metaConfig \"ilidata:{request.Profile.Id}\"";
        }

        /// <summary>
        /// Asynchronously executes the given <paramref name="command"/> on the Java runtime.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
        /// <returns>The exit code that the associated process specified when it terminated.</returns>
        private async Task<int> ExecuteJavaCommandAsync(string command, CancellationToken cancellationToken)
        {
            logger.LogInformation("Executing command: java {Command}", command);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = command,
                    RedirectStandardError = true,
                },
                EnableRaisingEvents = true,
            };

            process.Start();

            try
            {
                logger.LogTrace(await process.StandardError.ReadToEndAsync(cancellationToken));
                await process.WaitForExitAsync(cancellationToken);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancellation was requested
                process.Kill(entireProcessTree: true);
                return -1;
            }
        }
    }
}
