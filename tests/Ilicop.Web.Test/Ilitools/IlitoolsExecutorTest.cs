using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Geowerkstatt.Ilicop.Web.Ilitools
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

            ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = Path.Combine(TestContext.DeploymentDirectory, "FALLOUT"),
                CacheDir = Path.Combine(TestContext.DeploymentDirectory, "ARKSHARK"),
                ModelRepositoryDir = Path.Combine(TestContext.DeploymentDirectory, "OLYMPIAVIEW"),
                EnableGpkgValidation = true,
                IlivalidatorPath = "/path/to/ilivalidator.jar",
                Ili2GpkgPath = "/path/to/ili2gpkg.jar",
            };

            configuration = CreateConfiguration();
            ilitoolsExecutor = new IlitoolsExecutor(loggerMock.Object, ilitoolsEnvironment, configuration);
        }

        private IConfiguration CreateConfiguration(string commandFormat = "{0}")
        {
            return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Validation:CommandFormat", commandFormat },
                { "Validation:BlacklistedGpkgModels", "PEEVEDSCAN;WRONGPENGUIN;SPORKDEITY" },
            }).Build();
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

            var expected = $"java -jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithCatalogueFiles()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf", additionalCatalogueFilePaths: new List<string> { "additionalTestFile.xml" });
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"java -jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\" \"additionalTestFile.xml\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandWithModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg", "Model1;Model2");
            var command = ilitoolsExecutor.CreateIli2GpkgCommand(request);

            var expected = $"java -jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --models \"Model1;Model2\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --dbfile \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandWithoutModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg");
            var command = ilitoolsExecutor.CreateIli2GpkgCommand(request);

            var expected = $"java -jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --dbfile \"{request.TransferFilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithCustomCommandFormat()
        {
            var customConfig = CreateConfiguration("custom {0} wrapper");
            var customExecutor = new IlitoolsExecutor(loggerMock.Object, ilitoolsEnvironment, customConfig);
            var request = CreateValidationRequest("/test/path", "test.xtf");
            var command = string.Format(CultureInfo.InvariantCulture, "custom {0} wrapper", customExecutor.CreateIlivalidatorCommand(request));

            var expected = $"custom java -jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" \"{request.TransferFilePath}\" wrapper";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void ExecuteIlivalidatorAsyncFormatsCommandCorrectly()
        {
            var dummyRequest = CreateValidationRequest("/PEEVEDBAGEL/", "ANT.XTF");
            var customConfig = CreateConfiguration("dada hopp monkey:latest sh {0}");
            var customExecutor = new IlitoolsExecutor(loggerMock.Object, ilitoolsEnvironment, customConfig);
            var command = customExecutor.CreateIlivalidatorCommand(dummyRequest);
            var formattedCommand = string.Format(CultureInfo.InvariantCulture, "dada hopp monkey:latest sh {0}", command);

            StringAssert.Contains(formattedCommand, "dada hopp monkey:latest sh java -jar");
            StringAssert.Contains(formattedCommand, $"\"{dummyRequest.LogFilePath}\"");
            StringAssert.Contains(formattedCommand, $"\"{dummyRequest.XtfLogFilePath}\"");
            StringAssert.Contains(formattedCommand, $"\"{dummyRequest.TransferFilePath}\"");
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
