using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.XtfLog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Web;

namespace Geowerkstatt.Ilicop.Web.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class DownloadController : Controller
    {
        private readonly ILogger<DownloadController> logger;
        private readonly IFileProvider fileProvider;

        public DownloadController(ILogger<DownloadController> logger, IFileProvider fileProvider)
        {
            this.logger = logger;
            this.fileProvider = fileProvider;
        }

        /// <summary>
        /// Gets the ilivalidator log file for the specified <paramref name="jobId"/> and <paramref name="logType"/>.
        /// </summary>
        /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
        /// <param name="logType">The log type to download.</param>
        /// <returns>The ilivalidator log file.</returns>
        [HttpGet]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the ilivalidator log file.", ContentTypes = new[] { "text/xml; charset=utf-8", "application/geo+json" })]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ProblemDetails), "application/json")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The log file for the requested jobId cannot be found.", ContentTypes = new[] { "application/json" })]
        public IActionResult Download(Guid jobId, LogType logType)
        {
            fileProvider.Initialize(jobId);

            try
            {
                logger.LogInformation("Log file (<{LogType}>) for job identifier <{JobId}> requested.", HttpUtility.HtmlEncode(logType), jobId);
                var fileStream = fileProvider.OpenText(fileProvider.GetLogFile(logType)).BaseStream;
                return logType switch
                {
                    LogType.Log => File(fileStream, MediaTypeNames.Text.Plain),
                    LogType.Xtf => File(fileStream, "text/xml; charset=utf-8"),
                    LogType.Csv => File(fileStream, MediaTypeNames.Text.Csv),
                    LogType.GeoJson => File(fileStream, "application/geo+json"),
                    LogType.Zip => File(fileStream, "application/zip"),
                    _ => throw new NotSupportedException($"Log type <{logType}> is not supported."),
                };
            }
            catch (Exception)
            {
                Response.StatusCode = 404;
                return View("PageNotFound", "Die gesuchte Log-Datei wurde nicht gefunden. Möglicherweise wurde sie bereits gelöscht.");
            }
        }

        /// <summary>
        /// Gets the log data of the specified <paramref name="jobId"/> in JSON format.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns>The log data for the specified <paramref name="jobId"/>.</returns>
        [HttpGet("json")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the ilivalidator log data in JSON format.", typeof(IEnumerable<LogError>), "application/json")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The log file for the requested jobId cannot be found.", ContentTypes = new[] { "application/json" })]
        public IActionResult GetJsonLog(Guid jobId)
        {
            logger.LogTrace("JSON log for job <{JobId}> requested.", jobId);

            fileProvider.Initialize(jobId);

            try
            {
                var xtfLogFile = fileProvider.GetLogFile(LogType.Xtf);
                using var reader = fileProvider.OpenText(xtfLogFile);

                var result = XtfLogParser.Parse(reader);
                return Ok(result);
            }
            catch (FileNotFoundException)
            {
                return Problem($"No xtf log available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }
        }
    }
}
