using ILICheck.Web.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using static ILICheck.Web.ValidatorHelper;

namespace ILICheck.Web.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SettingsController : Controller
    {
        private readonly ILogger<SettingsController> logger;
        private readonly IConfiguration configuration;
        private readonly IlitoolsEnvironment ilitoolsEnvironment;

        public SettingsController(ILogger<SettingsController> logger, IConfiguration configuration, IlitoolsEnvironment ilitoolsEnvironment)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.ilitoolsEnvironment = ilitoolsEnvironment;
        }

        /// <summary>
        /// Gets the application settings.
        /// </summary>
        /// <returns>JSON-formatted client application settings.</returns>
        [HttpGet]
        [SwaggerResponse(StatusCodes.Status200OK, "The the application settings which can be used to configure a client.", typeof(SettingsResponse), "application/json")]
        public IActionResult GetSettings()
        {
            logger.LogTrace("Application configuration requested.");

            return Ok(new SettingsResponse
            {
                ApplicationName = configuration.GetValue<string>("CUSTOM_APP_NAME") ?? "INTERLIS Web-Check-Service",
                ApplicationVersion = configuration.GetValue<string>("ILICHECK_APP_VERSION") ?? "undefined",
                VendorLink = configuration.GetValue<string>("CUSTOM_VENDOR_LINK"),
                IlivalidatorVersion = ilitoolsEnvironment.IlivalidatorVersion ?? "undefined",
                Ili2gpkgVersion = ilitoolsEnvironment.EnableGpkgValidation
                    ? (ilitoolsEnvironment.Ili2GpkgVersion ?? "undefined/not configured")
                    : "disabled",
                AcceptedFileTypes = GetAcceptedFileExtensionsForUserUploads(configuration).JoinNonEmpty(", "),
            });
        }
    }
}
