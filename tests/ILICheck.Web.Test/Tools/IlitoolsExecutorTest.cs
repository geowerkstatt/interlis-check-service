using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ILICheck.Web.Tools
{
    [TestClass]
    public class IlitoolsExecutorTest
    {
        private Mock<ILogger<IlitoolsExecutor>> loggerMock;
        private IlitoolsEnvironment ilitoolsEnvironment;
        private IConfiguration configuration;
        private IlitoolsExecutor ilitoolsExecutor;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<IlitoolsExecutor>>();
            configuration = new ConfigurationBuilder().Build();

            ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = Path.Combine(TestContext.DeploymentDirectory, "FALLOUT"),
                CacheDir = Path.Combine(TestContext.DeploymentDirectory, "ARKSHARK"),
                ModelRepositoryDir = Path.Combine(TestContext.DeploymentDirectory, "OLYMPIAVIEW"),
                EnableGpkgValidation = true,
                IlivalidatorPath = "/path/to/ilivalidator.jar",
                Ili2GpkgPath = "/path/to/ili2gpkg.jar",
            };

            ilitoolsExecutor = new IlitoolsExecutor(loggerMock.Object, ilitoolsEnvironment, configuration);
        }

        [TestMethod]
        public void GetCommonIlitoolsArguments()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf");
            var args = string.Join(" ", ilitoolsExecutor.GetCommonIlitoolsArguments(request));

            Assert.AreEqual($"--log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\"", args);
        }

        [TestMethod]
        public void CreateIlivalidatorCommand()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf");
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --allObjectsAccessible --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithCatalogueFiles()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf", additionalCatalogueFilePaths: new List<string> { "additionalTestFile.xml" });
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --allObjectsAccessible --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\" \"additionalTestFile.xml\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithConfig()
        {
            ilitoolsEnvironment.IlivalidatorConfigPath = "/test/config.toml";
            var request = CreateValidationRequest("/test/path", "test.xtf");
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --allObjectsAccessible --config \"{ilitoolsEnvironment.IlivalidatorConfigPath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandWithModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg", "Model1;Model2");
            var command = ilitoolsExecutor.CreateIli2GpkgCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --models \"Model1;Model2\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --dbfile \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandWithoutModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg");
            var command = ilitoolsExecutor.CreateIli2GpkgCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --dbfile \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandIgnoresConfig()
        {
            ilitoolsEnvironment.IlivalidatorConfigPath = "/test/config.toml";
            var request = CreateValidationRequest("/test/path", "test.gpkg");
            var command = ilitoolsExecutor.CreateIli2GpkgCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --dbfile \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithSpecialPaths()
        {
            AssertIlivalidatorCommandContains("/PEEVEDBAGEL/", "ANT.XTF", null);
            AssertIlivalidatorCommandContains("foo/bar", "SETNET.GPKG", "ANGRY;SQUIRREL");
            AssertIlivalidatorCommandContains("$SEA/RED/", "WATCH.GPKG", string.Empty);
        }

        private void AssertIlivalidatorCommandContains(string homeDirectory, string transferFile, string modelNames)
        {
            var request = CreateValidationRequest(homeDirectory, transferFile, modelNames);
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            StringAssert.Contains(command, $"--log \"{request.LogFilePath}\"");
            StringAssert.Contains(command, $"--xtflog \"{request.XtfLogFilePath}\"");
            StringAssert.Contains(command, $"\"{request.TransferFilePath}\"");

            // Model names should not be included in ilivalidator command
            StringAssert.DoesNotMatch(command, new Regex("--models"));
        }

        private ValidationRequest CreateValidationRequest(string homeDirectory, string transferFile, string modelNames = null, List<string> additionalCatalogueFilePaths = null)
        {
            var transferFileNameWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);
            var logPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.log");
            var xtfLogPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.xtf");
            var transferFilePath = Path.Combine(homeDirectory, transferFile);

            return new ValidationRequest
            {
                TransferFileName = transferFile,
                TransferFilePath = transferFilePath,
                LogFilePath = logPath,
                XtfLogFilePath = xtfLogPath,
                GpkgModelNames = modelNames,
                AdditionalCatalogueFilePaths = additionalCatalogueFilePaths ?? new List<string>(),
            };
        }
    }
}
