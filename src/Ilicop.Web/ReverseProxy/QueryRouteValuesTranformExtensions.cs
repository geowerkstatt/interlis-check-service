using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Geowerkstatt.Ilicop.Web.ReverseProxy;

public static class QueryRouteValuesTranformExtensions
{
    /// <summary>
    /// Adds the transform that will append or set the query parameter from the given value.
    /// </summary>
    public static TransformBuilderContext AddQueryRouteValue(this TransformBuilderContext context, string queryKey, string pattern, bool append = true)
    {
        var binder = context.Services.GetRequiredService<TemplateBinderFactory>();
        context.RequestTransforms.Add(new QueryRouteValuesTransform(
            binder,
            queryKey,
            append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set,
            pattern));
        return context;
    }
}
