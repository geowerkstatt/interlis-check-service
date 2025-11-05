using Microsoft.AspNetCore.Routing.Constraints;
using System.IO;

namespace Geowerkstatt.Ilicop.Web;

/// <summary>
/// Represents a file that has an extra display name.
/// </summary>
/// <param name="FilePath">The file path.</param>
/// <param name="DisplayName">The display name of the file.</param>
public record NamedFile(string FilePath, string DisplayName)
{
    /// <summary>
    /// Gets the file name from the file path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedFile"/> class with the file name as the display name.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public NamedFile(string filePath)
        : this(filePath, Path.GetFileName(filePath))
    {
    }
}
