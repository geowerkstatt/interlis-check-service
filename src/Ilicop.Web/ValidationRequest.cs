using Geowerkstatt.Ilicop.Web.Ilitools;
using System;
using System.Collections.Generic;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Represents a request to validate an INTERLIS transfer file.
    /// </summary>
    public class ValidationRequest : IlitoolsRequest
    {
        /// <summary>
        /// Gets or sets the path to the CSV log file.
        /// </summary>
        public required string CsvLogFilePath { get; init; }

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
        public bool IsGeoPackage => FileName.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase);
    }
}
