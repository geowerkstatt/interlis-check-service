using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;

namespace Geowerkstatt.Ilicop.Web.Controllers
{
    [TestClass]
    public sealed class StatusControllerTest
    {
        private Mock<ILogger<StatusController>> loggerMock;
        private Mock<IValidatorService> validatorServiceMock;
        private Mock<IFileProvider> fileProviderMock;
        private Mock<ApiVersion> apiVersionMock;
        private Mock<IOptions<GwpProcessorOptions>> gwpProcessorOptionsMock;
        private Mock<IMapServiceUriGenerator> mapServiceUriGeneratorMock;
        private StatusController controller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<StatusController>>();
            validatorServiceMock = new Mock<IValidatorService>(MockBehavior.Strict);
            fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
            apiVersionMock = new Mock<ApiVersion>(MockBehavior.Strict, 8, 77);
            gwpProcessorOptionsMock = new Mock<IOptions<GwpProcessorOptions>>(MockBehavior.Strict);
            mapServiceUriGeneratorMock = new Mock<IMapServiceUriGenerator>(MockBehavior.Strict);

            gwpProcessorOptionsMock.SetupGet(x => x.Value)
                .Returns(new GwpProcessorOptions()
                {
                    QgisProjectFileName = "project.qgs",
                });

            fileProviderMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

            controller = new StatusController(
                loggerMock.Object,
                validatorServiceMock.Object,
                fileProviderMock.Object,
                gwpProcessorOptionsMock.Object,
                mapServiceUriGeneratorMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            loggerMock.VerifyAll();
            validatorServiceMock.VerifyAll();
            apiVersionMock.VerifyAll();

            controller.Dispose();
        }

        [TestMethod]
        public void GetStatus()
        {
            var jobId = new Guid("fadc5142-9043-4fdc-aebf-36c21e13f621");

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "SILENTFIRE_LOG.xtf" });
            fileProviderMock.SetupGet(x => x.HomeDirectory).Returns(new DirectoryInfo(TestContext.DeploymentDirectory));

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(jobId))))
                .Returns((Status.Processing, "WAFFLESPATULA GREENNIGHT"));

            var response = controller.GetStatus(apiVersionMock.Object, jobId) as OkObjectResult;

            Assert.IsInstanceOfType(response, typeof(OkObjectResult));
            Assert.IsInstanceOfType(response.Value, typeof(StatusResponse));
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(jobId, ((StatusResponse)response.Value).JobId);
            Assert.AreEqual(Status.Processing, ((StatusResponse)response.Value).Status);
            Assert.AreEqual("WAFFLESPATULA GREENNIGHT", ((StatusResponse)response.Value).StatusMessage);
            Assert.AreEqual(null, ((StatusResponse)response.Value).LogUrl);
            Assert.AreEqual($"/api/v8/download?jobId={jobId}&logType=xtf", ((StatusResponse)response.Value).XtfLogUrl.ToString());
            Assert.AreEqual($"/api/v8/download/json?jobId={jobId}", ((StatusResponse)response.Value).JsonLogUrl.ToString());
            Assert.IsNull(((StatusResponse)response.Value).GeoJsonLogUrl);
            Assert.IsNull(((StatusResponse)response.Value).MapServiceUrl);
        }

        [TestMethod]
        public void GetStatusWithGeoJson()
        {
            var jobId = new Guid("E0305248-907F-47FF-9B97-995EB4D92033");

            fileProviderMock.Setup(x => x.Initialize(jobId));
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "SILENTFIRE_LOG.xtf", "SILENTFIRE_LOG.geojson" });
            fileProviderMock.SetupGet(x => x.HomeDirectory).Returns(new DirectoryInfo(TestContext.DeploymentDirectory));

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(jobId))
                .Returns((Status.Processing, "WAFFLESPATULA GREENNIGHT"));

            var response = controller.GetStatus(apiVersionMock.Object, jobId) as OkObjectResult;

            Assert.IsInstanceOfType(response, typeof(OkObjectResult));
            Assert.IsInstanceOfType(response.Value, typeof(StatusResponse));
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual($"/api/v8/download?jobId={jobId}&logType=geojson", ((StatusResponse)response.Value).GeoJsonLogUrl.ToString());
        }

        [TestMethod]
        public void GetStatusForInvalid()
        {
            var jobId = new Guid("00000000-0000-0000-0000-000000000000");

            fileProviderMock.Setup(x => x.Initialize(It.Is<Guid>(x => x.Equals(jobId))));
            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(It.Is<Guid>(x => x.Equals(Guid.Empty))))
                .Returns((default, default));

            var response = controller.GetStatus(apiVersionMock.Object, default) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status404NotFound, response.StatusCode);
            Assert.AreEqual($"No job information available for job id <{jobId}>", ((ProblemDetails)response.Value).Detail);
        }

        [TestMethod]
        public void GetStatusWithMapService()
        {
            var jobId = new Guid("0ef30166-df4e-4eb4-8ef5-f355af39090d");

            fileProviderMock.Setup(x => x.Initialize(jobId));
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "SILENTFIRE_LOG.xtf" });
            fileProviderMock.Setup(x => x.Exists("project.qgs")).Returns(true);
            fileProviderMock.SetupGet(x => x.HomeDirectory).Returns(new DirectoryInfo(TestContext.DeploymentDirectory));

            validatorServiceMock
                .Setup(x => x.GetJobStatusOrDefault(jobId))
                .Returns((Status.Processing, "WAFFLESPATULA GREENNIGHT"));

            mapServiceUriGeneratorMock.Setup(x => x.BuildMapServiceUri(jobId))
                .Returns(new Uri($"/api/v8/mapservice/{jobId}", UriKind.Relative));

            var response = controller.GetStatus(apiVersionMock.Object, jobId) as OkObjectResult;

            Assert.IsInstanceOfType(response, typeof(OkObjectResult));
            Assert.IsInstanceOfType(response.Value, typeof(StatusResponse));
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(jobId, ((StatusResponse)response.Value).JobId);
            Assert.AreEqual($"/api/v8/mapservice/{jobId}", ((StatusResponse)response.Value).MapServiceUrl.ToString());
        }
    }
}
