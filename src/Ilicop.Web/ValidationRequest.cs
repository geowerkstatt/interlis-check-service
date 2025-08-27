using System;
using System.Collections.Generic;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Represents a request to validate an INTERLIS transfer file.
    /// </summary>
    public class ValidationRequest
    {
        /// <summary>
        /// Gets or sets the name of the transfer file to validate.
        /// </summary>
        public required string TransferFileName { get; init; }

        /// <summary>
        /// Gets or sets the full path to the transfer file.
        /// </summary>
        public required string TransferFilePath { get; init; }

        /// <summary>
        /// Gets or sets the path to the log file.
        /// </summary>
        public required string LogFilePath { get; init; }

        /// <summary>
        /// Gets or sets the path to the XTF log file.
        /// </summary>
        public required string XtfLogFilePath { get; init; }

        /// <summary>
        /// Gets or sets the GPKG model names (semicolon-separated) if validating a GeoPackage.
        /// </summary>
        public string GpkgModelNames { get; init; }

        /// <summary>
        /// Gets or sets additional catalogue files (full paths) to use during validation.
        /// </summary>
        public List<string> AdditionalCatalogueFilePaths { get; init; } = new List<string>();

        /// <summary>
        /// Gets a value indicating whether the file is a GeoPackage.
        /// </summary>
        public bool IsGeoPackage => TransferFileName.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase);
    }
}
