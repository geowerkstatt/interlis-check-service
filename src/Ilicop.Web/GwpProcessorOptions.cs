namespace Geowerkstatt.Ilicop.Web;

/// <summary>
/// Contains options to configure the GWP processor.
/// </summary>
public class GwpProcessorOptions
{
    /// <summary>
    /// The directory where GWP configuration files are located.
    /// The config directory is expected to contain a folder for each profile with the respective configuration files for that profile.
    /// </summary>
    public string ConfigDir { get; set; }

    /// <summary>
    /// The name of the folder within each profile's configuration directory that contains additional files to be included in the GWP ZIP archive:
    /// {ConfigDir}/{ProfileId}/{AdditionalFilesFolderName}.
    /// All files within this folder will be included in the ZIP archive automatically.
    /// </summary>
    public string AdditionalFilesFolderName { get; set; } = "AdditionalFiles";

    /// <summary>
    /// The name of the resulting ZIP file containing GWP results and logs.
    /// </summary>
    public string ZipFileName { get; set; } = "gwp_results_log.zip";
}
