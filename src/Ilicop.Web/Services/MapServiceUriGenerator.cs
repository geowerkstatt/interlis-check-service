using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Yarp.ReverseProxy;

namespace Geowerkstatt.Ilicop.Web.Services
{
    internal class MapServiceUriGenerator : IMapServiceUriGenerator
    {
        private readonly ILogger<MapServiceUriGenerator> logger;
        private readonly IOptions<MapServiceUriGenerationParameters> options;
        private readonly IProxyStateLookup proxyState;
        private readonly TemplateBinderFactory templateBinderFactory;

        public MapServiceUriGenerator(
            ILogger<MapServiceUriGenerator> logger,
            IOptions<MapServiceUriGenerationParameters> options,
            IProxyStateLookup proxyState,
            TemplateBinderFactory templateBinderFactory)
        {
            this.logger = logger;
            this.options = options;
            this.proxyState = proxyState;
            this.templateBinderFactory = templateBinderFactory;
        }

        public Uri BuildMapServiceUri(Guid jobId)
        {
            if (!proxyState.TryGetRoute(options.Value.MapServerRouteId, out var route) || string.IsNullOrEmpty(route.Config.Match.Path))
            {
                logger.LogInformation("No proxy route with key <{RouteKey}> found or route has no path configured. Cannot build map service URL for job <{JobId}>.", options.Value.MapServerRouteId, jobId);
                return null;
            }

            var template = TemplateParser.Parse(route.Config.Match.Path);
            if (!template.Parameters.Any(p => p.Name == options.Value.JobIdParameterName))
            {
                logger.LogInformation("No parameter {ParameterName} found in route template <{RouteTemplate}>. Cannot build map service URL for job <{JobId}>.", options.Value.MapServerRouteId, route.Config.Match.Path, jobId);
                return null;
            }

            var templateBinder = templateBinderFactory.Create(template, new());
            var values = new RouteValueDictionary
            {
                { options.Value.JobIdParameterName, jobId.ToString() },
            };
            var parameterizedRoute = templateBinder.BindValues(values);

            return string.IsNullOrEmpty(parameterizedRoute) ? null : new Uri(parameterizedRoute, UriKind.Relative);
        }
    }
}
