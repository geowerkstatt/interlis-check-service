using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ProfileController : Controller
    {
        private readonly ILogger<ProfileController> logger;
        private readonly IProfileService profileService;

        public ProfileController(ILogger<ProfileController> logger, IProfileService profileService)
        {
            this.logger = logger;
            this.profileService = profileService;
        }

        /// <summary>
        /// Gets all profiles.
        /// </summary>
        /// <returns>List of profiles.</returns>
        [HttpGet]
        [SwaggerResponse(StatusCodes.Status200OK, "All existing profiles.", typeof(IEnumerable<Profile>), "application/json")]
        public async Task<IActionResult> GetAll()
        {
            logger.LogTrace("Getting all profiles.");

            try
            {
                var profiles = await profileService.GetProfiles();
                return Ok(profiles);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not load profiles from repository.");

                return Problem(
                    detail: "Error while loading profiles",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error");
            }
        }
    }
}
