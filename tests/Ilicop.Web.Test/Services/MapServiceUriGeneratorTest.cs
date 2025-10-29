using Geowerkstatt.Ilicop.Web.Services;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Geowerkstatt.Ilicop.Services;

[TestClass]
public class MapServiceUriGeneratorTest
{
    private const string MapServerRouteId = "mapserver";
    private const string JobIdParameterName = "jobId";

    private MapServiceUriGenerator mapServiceUriGenerator;
    private Mock<ILogger<MapServiceUriGenerator>> loggerMock;
    private Mock<IOptions<MapServiceUriGenerationParameters>> optionsMock;
    private Mock<IProxyStateLookup> proxyStateMock;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new();
        optionsMock = new(MockBehavior.Strict);
        proxyStateMock = new(MockBehavior.Strict);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddRouting();
        using var services = serviceCollection.BuildServiceProvider();

        mapServiceUriGenerator = new MapServiceUriGenerator(
            loggerMock.Object,
            optionsMock.Object,
            proxyStateMock.Object,
            services.GetRequiredService<TemplateBinderFactory>());

        optionsMock.Setup(o => o.Value).Returns(new MapServiceUriGenerationParameters
        {
            MapServerRouteId = MapServerRouteId,
            JobIdParameterName = JobIdParameterName,
        });
    }

    [TestMethod]
    public void BuildMapServiceUriReplacesJobId()
    {
        var guid = new Guid("f59292d8-5dec-4e3d-a5bf-96cd52fd8bb9");
        var routeConfig = new RouteConfig
        {
            RouteId = MapServerRouteId,
            Match = new()
            {
                Path = $"/mapservice/{{{JobIdParameterName}}}/BEAMPLOW",
            },
        };
        var routeModel = new RouteModel(routeConfig, null, HttpTransformer.Empty);
        proxyStateMock.Setup(p => p.TryGetRoute(MapServerRouteId, out routeModel)).Returns(true);

        var uri = mapServiceUriGenerator.BuildMapServiceUri(guid);

        Assert.IsNotNull(uri);
        Assert.AreEqual("/mapservice/f59292d8-5dec-4e3d-a5bf-96cd52fd8bb9/BEAMPLOW", uri.OriginalString);
    }

    [TestMethod]
    public void BuildMapServiceUriReturnsNullIfRouteNotFound()
    {
        var guid = Guid.NewGuid();
        RouteModel routeModel = null;
        proxyStateMock.Setup(p => p.TryGetRoute(MapServerRouteId, out routeModel)).Returns(false);

        var uri = mapServiceUriGenerator.BuildMapServiceUri(guid);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public void BuildMapServiceuriReturnsNullIfRouteIsEmpty()
    {
        var guid = Guid.NewGuid();
        var routeConfig = new RouteConfig
        {
            RouteId = MapServerRouteId,
            Match = new()
            {
                Path = string.Empty,
            },
        };
        var routeModel = new RouteModel(routeConfig, null, HttpTransformer.Empty);
        proxyStateMock.Setup(p => p.TryGetRoute(MapServerRouteId, out routeModel)).Returns(true);

        var uri = mapServiceUriGenerator.BuildMapServiceUri(guid);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public void BuildMapServiceUriReturnsNullIfJobIdParameterNotFound()
    {
        var guid = Guid.NewGuid();
        var routeConfig = new RouteConfig
        {
            RouteId = MapServerRouteId,
            Match = new()
            {
                Path = "/mapservice/BEAMPLOW",
            },
        };
        var routeModel = new RouteModel(routeConfig, null, HttpTransformer.Empty);
        proxyStateMock.Setup(p => p.TryGetRoute(MapServerRouteId, out routeModel)).Returns(true);

        var uri = mapServiceUriGenerator.BuildMapServiceUri(guid);

        Assert.IsNull(uri);
    }
}
