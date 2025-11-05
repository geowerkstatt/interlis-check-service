using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;

namespace Geowerkstatt.Ilicop.Web;

public class ExportRequest : IlitoolsRequest
{
    public required string DbFilePath { get; init; }

    public required string Dataset { get; init; }
}
