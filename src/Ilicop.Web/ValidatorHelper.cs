using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Provides some <see cref="Validator"/> helper methods.
    /// </summary>
    public static class ValidatorHelper
    {
        /// <summary>
        /// Gets the accepted file extensions for user web uploads.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public static IEnumerable<string> GetAcceptedFileExtensionsForUserUploads(IConfiguration configuration)
        {
            var additionalExtensions = new[] { ".zip" };
            return GetOrderedTransferFileExtensions(configuration).Concat(additionalExtensions);
        }

        /// <summary>
        /// Gets the accepted file extensions for ZIP content.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public static IEnumerable<string> GetAcceptedFileExtensionsForZipContent(IConfiguration configuration)
        {
            var additionalExtensions = new[] { ".ili" };
            return GetOrderedTransferFileExtensions(configuration).Concat(additionalExtensions);
        }

        /// <summary>
        /// Gets the transfer file extensions which are supported for validation with ilivalidator.
        /// The ordered list of transfer file extensions is prioritized according to known rules
        /// when validating with additional files (eg. catalogues).
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public static IEnumerable<string> GetOrderedTransferFileExtensions(IConfiguration configuration)
        {
            foreach (var extension in new[] { ".xtf", ".itf", ".xml" })
            {
                yield return extension;
            }

            if (configuration.GetValue<bool>("ENABLE_GPKG_VALIDATION")) yield return ".gpkg";
        }
    }
}
