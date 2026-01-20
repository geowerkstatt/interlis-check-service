using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILICheck.Web.Tools
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
        internal List<string> CreateIlivalidatorCommand(ValidationRequest request)
        {
            var args = new List<string>
            {
                "-jar",
                ilitoolsEnvironment.IlivalidatorPath,
                "--allObjectsAccessible",
            };

            // Add plugins
            if (Directory.Exists(ilitoolsEnvironment.PluginsDir))
            {
                var jarFiles = Directory.GetFiles(ilitoolsEnvironment.PluginsDir, "*.jar", SearchOption.TopDirectoryOnly);
                if (jarFiles.Length > 0)
                {
                    args.Add("--plugins");
                    args.Add(ilitoolsEnvironment.PluginsDir);
                    logger.LogDebug("Added plugins directory with {PluginCount} JAR files", jarFiles.Length);
                }
            }

            if (!string.IsNullOrEmpty(ilitoolsEnvironment.IlivalidatorConfigPath))
            {
                args.Add("--config");
                args.Add(ilitoolsEnvironment.IlivalidatorConfigPath);
            }

            args.AddRange(GetCommonIlitoolsArguments(request));

            // Add transfer file path (without specific parameter name)
            args.Add(request.TransferFilePath);

            // Add additional catalogue files if present
            foreach (var cataloguePath in request.AdditionalCatalogueFilePaths)
            {
                args.Add(cataloguePath);
            }

            return args;
        }

        /// <summary>
        /// Creates the command for executing ili2gpkg.
        /// </summary>
        internal List<string> CreateIli2GpkgCommand(ValidationRequest request)
        {
            var args = new List<string>
            {
                "-jar",
                ilitoolsEnvironment.Ili2GpkgPath,
                "--validate",
            };

            // Add model names for GPKG files if specified
            if (!string.IsNullOrEmpty(request.GpkgModelNames))
            {
                args.Add("--models");
                args.Add(request.GpkgModelNames);
            }

            args.AddRange(GetCommonIlitoolsArguments(request));

            // Add database file parameter
            args.Add("--dbfile");
            args.Add(request.TransferFilePath);

            return args;
        }

        internal IEnumerable<string> GetCommonIlitoolsArguments(ValidationRequest request)
        {
            // Add common logging options
            yield return "--log";
            yield return request.LogFilePath;
            yield return "--xtflog";
            yield return request.XtfLogFilePath;
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
                    yield return "--proxy";
                    yield return uri.Host;
                }

                if (uri?.Port != -1)
                {
                    yield return "--proxyPort";
                    yield return uri.Port.ToString(CultureInfo.InvariantCulture);
                }
            }

            // Add trace if enabled
            if (ilitoolsEnvironment.TraceEnabled)
            {
                yield return "--trace";
            }

            // Add model directory
            if (!string.IsNullOrEmpty(ilitoolsEnvironment.ModelRepositoryDir))
            {
                yield return "--modeldir";
                yield return ilitoolsEnvironment.ModelRepositoryDir;
            }
        }

        /// <summary>
        /// Asynchronously executes the given <paramref name="command"/> on the Java runtime.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
        /// <returns>The exit code that the associated process specified when it terminated.</returns>
        private async Task<int> ExecuteJavaCommandAsync(List<string> command, CancellationToken cancellationToken)
        {
            logger.LogInformation("Executing command: java {Command}", PrettyPrintCommand(command));

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("java", command)
                {
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

        /// <summary>
        /// Pretty-prints the given command arguments for logging purposes.
        /// Caution: this is not intended to be safe to use as process arguments.
        /// </summary>
        internal static string PrettyPrintCommand(IEnumerable<string> command)
        {
            // No quotes around commandline options
            return command
                .Select(arg => arg.StartsWith('-') ? arg : $"\"{arg}\"")
                .JoinNonEmpty(" ");
        }
    }
}
