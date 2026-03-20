using System.ComponentModel.DataAnnotations;

namespace Geowerkstatt.Ilicop.Web.Contracts
{
    /// <summary>
    /// The settings response schema.
    /// </summary>
    public class SettingsResponse
    {
        /// <summary>
        /// The application name.
        /// </summary>
        [Required]
        public string ApplicationName { get; set; }

        /// <summary>
        /// The application version.
        /// </summary>
        [Required]
        public string ApplicationVersion { get; set; }

        /// <summary>
        /// The vendor link if available; otherwise, <c>null</c>.
        /// </summary>
        public string VendorLink { get; set; }

        /// <summary>
        /// The ilivalidator version.
        /// </summary>
        [Required]
        public string IlivalidatorVersion { get; set; }

        /// <summary>
        /// The ili2gpkg version.
        /// </summary>
        [Required]
        public string Ili2gpkgVersion { get; set; }

        /// <summary>
        /// The accepted file types.
        /// </summary>
        [Required]
        public string AcceptedFileTypes { get; set; }

        /// <summary>
        /// The maximum upload size in bytes if configured; otherwise, <c>null</c>.
        /// </summary>
        public int? MaxUploadSizeBytes { get; set; }
    }
}
