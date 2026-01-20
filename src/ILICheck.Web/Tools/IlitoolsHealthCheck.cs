using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ILICheck.Web.Tools
{
    /// <summary>
    /// Represents a health check, which is used to check the status of the ilitools.
    /// </summary>
    public class IlitoolsHealthCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;
        private readonly ILogger logger;
        private readonly IlitoolsEnvironment ilitoolsEnvironment;

        /// <summary>
        /// Initializes a new instance of the <see cref="IlitoolsHealthCheck"/> class.
        /// </summary>
        public IlitoolsHealthCheck(IConfiguration configuration, ILogger<IlitoolsHealthCheck> logger, IlitoolsEnvironment ilitoolsEnvironment)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.ilitoolsEnvironment = ilitoolsEnvironment;
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ilitoolsEnvironment.IsIlivalidatorInitialized)
                {
                    logger.LogError("Ilivalidator is not properly initialized.");
                    return await Task.FromResult(HealthCheckResult.Unhealthy());
                }

                if (ilitoolsEnvironment.EnableGpkgValidation && !ilitoolsEnvironment.IsIli2GpkgInitialized)
                {
                    logger.LogError("ili2gpkg expected but not initialized.");
                    return await Task.FromResult(HealthCheckResult.Degraded());
                }

                return await Task.FromResult(HealthCheckResult.Healthy());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while checking ilivalidator health.");
                return await Task.FromResult(HealthCheckResult.Unhealthy());
            }
        }
    }
}
