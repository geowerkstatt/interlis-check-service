namespace ILICheck.Web.Tools
{
    /// <summary>
    /// Runtime-populated metadata about installed ilitools.
    /// Populated by <see cref="IlitoolsBootstrapService"/> during application startup.
    /// </summary>
    public class IlitoolsEnvironment
    {
        /// <summary>
        /// Gets the installation directory.
        /// </summary>
        public string InstallationDir { get; init; }

        /// <summary>
        /// Gets the cache directory.
        /// </summary>
        public string CacheDir { get; init; }

        /// <summary>
        /// Gets the local model repository directory.
        /// </summary>
        public string ModelRepositoryDir { get; init; }

        /// <summary>
        /// Gets the plugins directory.
        /// </summary>
        public string PluginsDir { get; init; }

        /// <summary>
        /// Indicates whether the GPKG validation should be enabled.
        /// </summary>
        public bool EnableGpkgValidation { get; init; }

        /// <summary>
        /// Gets or sets the ilivalidator version.
        /// </summary>
        public string IlivalidatorVersion { get; set; }

        /// <summary>
        /// Gets or sets the path to the ilivalidator tool.
        /// </summary>
        public string IlivalidatorPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the optional ilivalidator TOML configuration file.
        /// </summary>
        public string IlivalidatorConfigPath { get; set; }

        /// <summary>
        /// Gets or sets the ili2gpkg version.
        /// </summary>
        public string Ili2GpkgVersion { get; set; }

        /// <summary>
        /// Gets or sets the path to the ili2gpkg tool.
        /// </summary>
        public string Ili2GpkgPath { get; set; }

        /// <summary>
        /// Indicates whether the trace logging is enabled.
        /// </summary>
        public bool TraceEnabled { get; internal set; }

        /// <summary>
        /// Indicates whether the ilivalidator tool is properly setup and initialized.
        /// </summary>
        public bool IsIlivalidatorInitialized => !string.IsNullOrWhiteSpace(IlivalidatorPath);

        /// <summary>
        /// Indicates whether the ili2gpkg tool is properly setup and initialized.
        /// </summary>
        public bool IsIli2GpkgInitialized => EnableGpkgValidation && !string.IsNullOrWhiteSpace(Ili2GpkgPath);

        /// <inheritdoc/>
        public override string ToString()
        {
            return $$"""

    --------------------------------------------------------------------------
    ilitools environment:
    home directory:                   {{InstallationDir ?? "unset"}}
    cache directory:                  {{CacheDir ?? "unset"}}
    model repository directory:       {{ModelRepositoryDir ?? "unset"}}
    plugins directory:                {{PluginsDir ?? "unset"}}
    gpkg validation:                  {{(EnableGpkgValidation ? "enabled" : "disabled")}}
    ilivalidator version:             {{IlivalidatorVersion ?? "unset"}}
    ilivalidator initialized:         {{(IsIlivalidatorInitialized ? "yes" : "no")}}
    ili2gpkg version:                 {{Ili2GpkgVersion ?? "unset"}}
    ili2gpkg initialized:             {{(IsIli2GpkgInitialized ? "yes" : "no")}}
    trace messages enabled:           {{(TraceEnabled ? "yes" : "no")}}
    --------------------------------------------------------------------------

    """;
        }
    }
}
