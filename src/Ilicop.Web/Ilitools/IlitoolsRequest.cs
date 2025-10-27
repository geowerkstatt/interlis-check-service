using Geowerkstatt.Ilicop.Web.Contracts;

namespace Geowerkstatt.Ilicop.Web.Ilitools;

public abstract class IlitoolsRequest
{
    /// <summary>
    /// Gets the name of the file to be processed.
    /// </summary>
    public string FileName { get; init; }

    /// <summary>
    /// Gets the path to the file to be processed.
    /// </summary>
    public string FilePath { get; init; }

    /// <summary>
    /// Gets the profile with which the processing should be done.
    /// </summary>
    public Profile Profile { get; init; }

    /// <summary>
    /// Gets the path to the log file.
    /// </summary>
    public string LogFilePath { get; init; }

    /// <summary>
    /// Gets the path to the XTF log file.
    /// </summary>
    public string XtfLogFilePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether verbose logging is enabled.
    /// </summary>
    public bool VerboseLogging { get; init; }
}
