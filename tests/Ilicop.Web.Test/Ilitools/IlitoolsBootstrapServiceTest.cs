using Geowerkstatt.Ilicop.Web.Ilitools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Ilitools
{
    [TestClass]
    [DeploymentItem("testdata/mock.zip")]
    [DeploymentItem("testdata/ilitools", "RAGESLAW")]
    public class IlitoolsBootstrapServiceTest
    {
        private Mock<ILogger<IlitoolsBootstrapService>> loggerMock;
        private Mock<HttpMessageHandler> httpMessageHandlerMock;
        private HttpClient httpClient;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<IlitoolsBootstrapService>>();
            httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpClient = new HttpClient(httpMessageHandlerMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            httpClient?.Dispose();
        }

        [TestMethod]
        public async Task StartAsyncWithSpecificVersionSetsEnvironmentVariable()
        {
            var ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = Path.Combine(TestContext.DeploymentDirectory, "FALLOUT"),
                CacheDir = Path.Combine(TestContext.DeploymentDirectory, "ARKSHARK"),
                EnableGpkgValidation = false,
            };

            var configValues = new Dictionary<string, string> { { "ILIVALIDATOR_VERSION", "0.0.0" } };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Setup HTTP response for download
            using var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(File.ReadAllBytes("mock.zip")),
            };

            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var service = new IlitoolsBootstrapService(loggerMock.Object, configuration, httpClient, ilitoolsEnvironment);

            // Clear any existing environment variables
            Environment.SetEnvironmentVariable("ILIVALIDATOR_VERSION", null);
            Environment.SetEnvironmentVariable("ILI_CACHE", null);

            await service.StartAsync(CancellationToken.None);

            // Verify environment variables are set
            Assert.AreEqual("0.0.0", Environment.GetEnvironmentVariable("ILIVALIDATOR_VERSION"));
            Assert.AreEqual(Path.Combine(TestContext.DeploymentDirectory, "ARKSHARK"), Environment.GetEnvironmentVariable("ILI_CACHE"));

            // Verify files were extracted
            var installDir = Path.Combine(TestContext.DeploymentDirectory, "FALLOUT", "ilivalidator", "0.0.0");
            Assert.IsTrue(Directory.Exists(installDir), "Install directory should exist");

            var files = Directory.GetFiles(installDir, "*", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "Files should be extracted");
        }

        [TestMethod]
        public async Task StartAsyncWithGpkgEnabledBootstrapsBothTools()
        {
            var ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = "PICARESQUEOASIS",
                EnableGpkgValidation = true,
            };

            var configValues = new Dictionary<string, string>
            {
                { "ILIVALIDATOR_VERSION", "77.33.0" },
                { "ILI2GPKG_VERSION", "5.999.7" },
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Setup HTTP responses for both downloads
            using var ilivalidatorResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(File.ReadAllBytes("mock.zip")),
            };

            using var ili2gpkgResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(File.ReadAllBytes("mock.zip")),
            };

            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("ilivalidator")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ilivalidatorResponse);

            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("ili2gpkg")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(ili2gpkgResponse);

            var service = new IlitoolsBootstrapService(loggerMock.Object, configuration, httpClient, ilitoolsEnvironment);

            // Clear any existing environment variables
            Environment.SetEnvironmentVariable("ILIVALIDATOR_VERSION", null);
            Environment.SetEnvironmentVariable("ILI2GPKG_VERSION", null);

            await service.StartAsync(CancellationToken.None);

            // Verify both environment variables are set
            Assert.AreEqual("77.33.0", Environment.GetEnvironmentVariable("ILIVALIDATOR_VERSION"));
            Assert.AreEqual("5.999.7", Environment.GetEnvironmentVariable("ILI2GPKG_VERSION"));
        }

        [TestMethod]
        public async Task StartAsyncSkipsAlreadyInstalledTool()
        {
            var ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = "STELLARWITCH",
                EnableGpkgValidation = false,
            };

            var configValues = new Dictionary<string, string> { { "ILIVALIDATOR_VERSION", "0.0.0" } };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Pre-create the install directory
            var installDir = Path.Combine(TestContext.DeploymentDirectory, "STELLARWITCH", "ilivalidator", "0.0.0");
            Directory.CreateDirectory(installDir);
            ZipFile.ExtractToDirectory("mock.zip", installDir, overwriteFiles: true);

            var service = new IlitoolsBootstrapService(loggerMock.Object, configuration, httpClient, ilitoolsEnvironment);

            // Clear environment variable
            Environment.SetEnvironmentVariable("ILIVALIDATOR_VERSION", null);

            await service.StartAsync(CancellationToken.None);

            // Verify environment variable is still set even when skipping download
            Assert.AreEqual("0.0.0", Environment.GetEnvironmentVariable("ILIVALIDATOR_VERSION"));

            // Verify no HTTP requests were made
            httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        [DataRow("ilivalidator", "5.10.9")]
        [DataRow("ili2gpkg", "7.7.7")]
        [DataRow("KIMBOHUNT", null)]
        public void GetLatestInstalledIlitoolVersion(string ilitool, string expectedLatestVersion)
        {
            var ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = "RAGESLAW",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            var service = new IlitoolsBootstrapService(loggerMock.Object, configuration, httpClient, ilitoolsEnvironment);
            Assert.AreEqual(expectedLatestVersion, service.GetLatestInstalledIlitoolVersion(ilitool));
        }
    }
}
