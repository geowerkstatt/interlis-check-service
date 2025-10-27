using Geowerkstatt.Ilicop.Web.Ilitools;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace Geowerkstatt.Ilicop.Web;

[TestClass]
public class GwpProcessorTest
{
    private GwpProcessor processor;

    [TestInitialize]
    public void Initialize()
    {
        var optionsMock = new Mock<IOptions<GwpProcessorOptions>>();
        var fileProviderMock = new Mock<IFileProvider>();
        var loggerMock = new Mock<ILogger<GwpProcessor>>();

        var ilitoolsExecutor = new IlitoolsExecutor(new Mock<ILogger<IlitoolsExecutor>>().Object, new IlitoolsEnvironment(), new Mock<IConfiguration>().Object);

        optionsMock.SetupGet(x => x.Value).Returns(new GwpProcessorOptions
        {
            ConfigDir = "config",
            AdditionalFilesFolderName = "AdditionalFiles",
            DataGpkgFileName = "data.gpkg",
            ZipFileName = "result.zip",
        });

        processor = new GwpProcessor(optionsMock.Object, fileProviderMock.Object, ilitoolsExecutor, loggerMock.Object);
    }

    [TestMethod]
    [DataRow(
        new string[] { "ModelA.Topic1", "ModelB.Topic2", "ModelC.Topic3" },
        new string[] { "ModelA", "ModelB" },
        new string[] { "ModelC" })
    ]
    [DataRow(
        new string[] { "ModelA.Topic1", "ModelB.Topic2" },
        new string[] { "ModelA", "ModelB" },
        new string[] { })
    ]
    [DataRow(
        new string[] { "ModelA.Topic1", "ModelB.Topic2", "ModelZ.Topic3" },
        new string[] { "ModelA{ ModelB ModelC ModelD}", "ModelE" },
        new string[] { "ModelZ" })
    ]
    [DataRow(
        new string[] { "ModelA.Topic1", "ModelB.Topic2", "ModelE.Topic3", "ModelF.Topic4", "ModelG.Topic5", "ModelI.Topic6" },
        new string[] { "ModelA{ ModelB ModelC ModelD} ModelE ModelF{ ModelG ModelH}", "ModelI" },
        new string[] { })
    ]
    public void GetBasketTopicsNotInModels(IEnumerable<string> topics, IEnumerable<string> models, IEnumerable<string> expectedResult)
    {
        var actualResult = processor.GetBasketTopicsNotInModels(topics, models).ToList();

        CollectionAssert.AreEqual(expectedResult.ToList(), actualResult);
    }
}
