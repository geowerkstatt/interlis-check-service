using System;
using System.ComponentModel.DataAnnotations;

namespace Geowerkstatt.Ilicop.Web.Contracts
{
    /// <summary>
    /// The status response schema.
    /// </summary>
    public class StatusResponse
    {
        /// <summary>
        /// The job identification.
        /// </summary>
        [Required]
        public Guid JobId { get; set; }

        /// <summary>
        /// The job status.
        /// </summary>
        [Required]
        public Status Status { get; set; }

        /// <summary>
        /// The job status message.
        /// </summary>
        [Required]
        public string StatusMessage { get; set; }

        /// <summary>
        /// The log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
        /// </summary>
        public Uri LogUrl { get; set; }

        /// <summary>
        /// The XTF log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
        /// </summary>
        public Uri XtfLogUrl { get; set; }

        /// <summary>
        /// The CSV log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
        /// </summary>
        public Uri CsvLogUrl { get; set; }

        /// <summary>
        /// The JSON log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
        /// </summary>
        public Uri JsonLogUrl { get; set; }

        /// <summary>
        /// The GeoJSON log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
        /// </summary>
        public Uri GeoJsonLogUrl { get; set; }
    }
}
