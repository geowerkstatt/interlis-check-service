using Geowerkstatt.Ilicop.Web.Ilitools;

namespace Geowerkstatt.Ilicop.Web;

public class ImportRequest : IlitoolsRequest
{
    /// <summary>
    /// Gets or sets the full path to the database file to import data into.
    /// </summary>
    public required string DbFilePath { get; init; }
}
