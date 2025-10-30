using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using System;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Transforms;

namespace Geowerkstatt.Ilicop.Web.ReverseProxy;

/// <summary>
/// Generates a new query parameter by plugging matched route parameters into the given pattern.
/// Combination from <see cref="PathRouteValuesTransform"/> and <see cref="QueryParameterTransform"/>.
/// </summary>
public class QueryRouteValuesTransform : QueryParameterTransform
{
    private readonly TemplateBinderFactory binderFactory;

    internal RoutePattern Pattern { get; }

    public QueryRouteValuesTransform(TemplateBinderFactory binderFactory, string key, QueryStringTransformMode mode, [StringSyntax("Route")] string pattern)
        : base(mode, key)
    {
        ArgumentNullException.ThrowIfNull(binderFactory);
        ArgumentNullException.ThrowIfNull(pattern);

        Pattern = RoutePatternFactory.Parse(pattern);
        this.binderFactory = binderFactory;
    }

    protected override string GetValue(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // TemplateBinder.BindValues will modify the RouteValueDictionary
        // We make a copy so that the original request is not modified by the transform
        var routeValues = context.HttpContext.Request.RouteValues;
        var routeValuesCopy = new RouteValueDictionary();

        // Only copy route values used in the pattern, otherwise they'll be added as query parameters.
        foreach (var pattern in Pattern.Parameters)
        {
            if (routeValues.TryGetValue(pattern.Name, out var value))
            {
                routeValuesCopy[pattern.Name] = value;
            }
        }

        var binder = binderFactory.Create(Pattern);
        return binder.BindValues(acceptedValues: routeValuesCopy);
    }
}
