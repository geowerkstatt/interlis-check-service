using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

namespace Geowerkstatt.Ilicop.Web.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class StatusController : Controller
    {
        private readonly ILogger<StatusController> logger;
        private readonly IValidatorService validatorService;
        private readonly IFileProvider fileProvider;
        private readonly IOptions<GwpProcessorOptions> gwpProcessorOptions;
        private readonly IProxyStateLookup proxyState;
        private readonly TemplateBinderFactory templateBinderFactory;

        public StatusController(
            ILogger<StatusController> logger,
            IValidatorService validatorService,
            IFileProvider fileProvider,
            IOptions<GwpProcessorOptions> gwpProcessorOptions,
            IProxyStateLookup proxyState,
            TemplateBinderFactory templateBinderFactory)
        {
            this.logger = logger;
            this.validatorService = validatorService;
            this.fileProvider = fileProvider;
            this.gwpProcessorOptions = gwpProcessorOptions;
            this.proxyState = proxyState;
            this.templateBinderFactory = templateBinderFactory;
        }

        /// <summary>
        /// Gets the status information for the specified <paramref name="jobId"/>.
        /// </summary>
        /// <param name="version">The application programming interface (API) version.</param>
        /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
        /// <returns>The status information for the specified <paramref name="jobId"/>.</returns>
        [HttpGet("{jobId}")]
        [SwaggerResponse(StatusCodes.Status200OK, "The job with the specified jobId was found.", typeof(StatusResponse), "application/json")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), "application/json")]
        public IActionResult GetStatus(ApiVersion version, Guid jobId)
        {
            logger.LogTrace("Status for job <{JobId}> requested.", jobId);

            fileProvider.Initialize(jobId);

            var job = validatorService.GetJobStatusOrDefault(jobId);
            if (job == default)
            {
                return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }

            var xtfLogUrl = GetLogDownloadUrl(version, jobId, LogType.Xtf);
            var csvLogUrl = GetLogDownloadUrl(version, jobId, LogType.Csv);
            return Ok(new StatusResponse
            {
                JobId = jobId,
                Status = job.Status,
                StatusMessage = job.StatusMessage,
                LogUrl = GetLogDownloadUrl(version, jobId, LogType.Log),
                XtfLogUrl = xtfLogUrl,
                ZipUrl = GetLogDownloadUrl(version, jobId, LogType.Zip),
                CsvLogUrl = csvLogUrl,
                JsonLogUrl = xtfLogUrl == null ? null : GetJsonLogUrl(version, jobId), // JSON is generated from the XTF log file
                GeoJsonLogUrl = GetLogDownloadUrl(version, jobId, LogType.GeoJson),
                MapServiceUrl = GetMapServiceUrl(jobId, fileProvider),
            });
        }

        /// <summary>
        /// Gets the map service URL for the specified <paramref name="jobId"/> if any a service is available.
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="fileProvider"></param>
        /// <returns>Relative Url of the configured QGIS server proxy route.</returns>
        private Uri GetMapServiceUrl(Guid jobId, IFileProvider fileProvider)
        {
            if (!fileProvider.Exists(gwpProcessorOptions.Value.QgisProjectFileName))
                return null;

            if (!proxyState.TryGetRoute("validationMapserverRoute", out var route) || string.IsNullOrEmpty(route.Config.Match.Path))
                return null;

            var template = TemplateParser.Parse(route.Config.Match.Path);
            if (!template.Parameters.Any(p => p.Name.Equals("jobId", StringComparison.OrdinalIgnoreCase)))
                return null;

            var templateBinder = templateBinderFactory.Create(template, new());
            var values = new RouteValueDictionary
            {
                { "jobId", jobId.ToString() },
            };
            var parameterizedRoute = templateBinder.BindValues(values);

            return string.IsNullOrEmpty(parameterizedRoute) ? null : new Uri(parameterizedRoute, UriKind.Relative);
        }

        /// <summary>
        /// Gets the log download URL for the specified <paramref name="logType"/>.
        /// </summary>
        /// <param name="version">The application programming interface (API) version.</param>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="logType">The log type to download.</param>
        /// <returns>The log download URL if the log file exists; otherwise, <c>null</c>.</returns>
        internal Uri GetLogDownloadUrl(ApiVersion version, Guid jobId, LogType logType)
        {
            try
            {
                _ = fileProvider.GetLogFile(logType);
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            var downloadLogUrlTemplate = "/api/v{0}/download?jobId={1}&logType={2}";
            return new Uri(
                string.Format(
                    CultureInfo.InvariantCulture,
                    downloadLogUrlTemplate,
                    version.MajorVersion,
                    jobId,
                    logType.ToString().ToLowerInvariant()),
                UriKind.Relative);
        }

        private Uri GetJsonLogUrl(ApiVersion version, Guid jobId)
        {
            var logUrlTemplate = "/api/v{0}/download/json?jobId={1}";
            return new Uri(
                string.Format(
                    CultureInfo.InvariantCulture,
                    logUrlTemplate,
                    version.MajorVersion,
                    jobId),
                UriKind.Relative);
        }
    }
}
