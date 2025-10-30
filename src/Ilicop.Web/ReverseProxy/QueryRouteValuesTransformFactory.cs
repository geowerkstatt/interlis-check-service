using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Geowerkstatt.Ilicop.Web.ReverseProxy;

internal sealed class QueryRouteValuesTransformFactory : ITransformFactory
{
    internal const string QueryRouteValuesKey = "QueryRouteValues";
    internal const string AppendKey = "Append";
    internal const string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(QueryRouteValuesKey, out var queryValueParameter))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryRouteValues: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(QueryRouteValuesKey, out var queryValueParameter))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddQueryRouteValue(queryValueParameter, appendValue, append: true);
            }
            else if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddQueryRouteValue(queryValueParameter, setValue, append: false);
            }
            else
            {
                throw new NotSupportedException(string.Join(";", transformValues.Keys));
            }
        }
        else
        {
            return false;
        }

        return true;
    }
}
