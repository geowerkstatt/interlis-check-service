using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
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
            var request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT");
            var args = string.Join(" ", ilitoolsExecutor.GetCommonIlitoolsArguments(request));

            Assert.AreEqual($"--log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:DEFAULT\"", args);
        }

        [TestMethod]
        public void GetCommonIlitoolsArgumentsWithoutLogging()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT", verboseLogging: false, log: true, xtfLog: true);
            var args = string.Join(" ", ilitoolsExecutor.GetCommonIlitoolsArguments(request));
            Assert.IsFalse(args.Contains("--verbose"));
            Assert.IsTrue(args.Contains("--log"));
            Assert.IsTrue(args.Contains("--xtflog"));

            request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT", verboseLogging: false, log: false, xtfLog: true);
            args = string.Join(" ", ilitoolsExecutor.GetCommonIlitoolsArguments(request));
            Assert.IsFalse(args.Contains("--log"));
            Assert.IsTrue(args.Contains("--xtflog"));

            request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT", verboseLogging: false, log: false, xtfLog: false);
            args = string.Join(" ", ilitoolsExecutor.GetCommonIlitoolsArguments(request));
            Assert.IsFalse(args.Contains("--xtflog"));
        }

        [TestMethod]
        public void CreateIlivalidatorCommand()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT");
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --csvlog \"{request.CsvLogFilePath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:DEFAULT\" \"{request.FilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIlivalidatorCommandWithCatalogueFiles()
        {
            var request = CreateValidationRequest("/test/path", "test.xtf", "DEFAULT", additionalCatalogueFilePaths: new List<string> { "additionalTestFile.xml" });
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.IlivalidatorPath}\" --csvlog \"{request.CsvLogFilePath}\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:DEFAULT\" \"{request.FilePath}\" \"additionalTestFile.xml\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgCommandWithModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg", "DEFAULT", "Model1;Model2");
            var command = ilitoolsExecutor.CreateIli2GpkgValidationCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --models \"Model1;Model2\" --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:DEFAULT\" --dbfile \"{request.FilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgValidationCommandWithoutModelNames()
        {
            var request = CreateValidationRequest("/test/path", "test.gpkg", "DEFAULT");
            var command = ilitoolsExecutor.CreateIli2GpkgValidationCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --validate --log \"{request.LogFilePath}\" --xtflog \"{request.XtfLogFilePath}\" --verbose --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:DEFAULT\" --dbfile \"{request.FilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgValidationCommandWithSpecialPaths()
        {
            AssertIlivalidatorCommandContains("/PEEVEDBAGEL/", "ANT.XTF", null);
            AssertIlivalidatorCommandContains("foo/bar", "SETNET.GPKG", "ANGRY;SQUIRREL");
            AssertIlivalidatorCommandContains("$SEA/RED/", "WATCH.GPKG", string.Empty);
        }

        [TestMethod]
        public void CreateIli2GpkgImportCommand()
        {
            var request = new ImportRequest
            {
                FileName = "import.xtf",
                FilePath = "/import/path/import.gpkg",
                DbFilePath = "/import/path/import.gpkg",
                Profile = new Profile { Id = "DEFAULT" },
                Dataset = "Data",
            };

            var command = ilitoolsExecutor.CreateIli2GpkgImportCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --import --disableValidation --skipReferenceErrors --skipGeometryErrors --dataset \"{request.Dataset}\" --dbfile \"{request.DbFilePath}\" --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:{request.Profile.Id}\" \"{request.FilePath}\"";
            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void CreateIli2GpkgExportCommand()
        {
            var request = new ExportRequest
            {
                FileName = "export.xtf",
                FilePath = "/export/path/export.gpkg",
                DbFilePath = "/export/path/export.gpkg",
                Profile = new Profile { Id = "DEFAULT" },
                Dataset = "Data",
            };

            var command = ilitoolsExecutor.CreateIli2GpkgExportCommand(request);

            var expected = $"-jar \"{ilitoolsEnvironment.Ili2GpkgPath}\" --export --disableValidation --skipReferenceErrors --skipGeometryErrors --dataset \"{request.Dataset}\" --dbfile \"{request.DbFilePath}\" --modeldir \"{ilitoolsEnvironment.ModelRepositoryDir}\" --metaConfig \"ilidata:{request.Profile.Id}\" \"{request.FilePath}\"";
            Assert.AreEqual(expected, command);
        }

        private void AssertIlivalidatorCommandContains(string homeDirectory, string transferFile, string modelNames)
        {
            var request = CreateValidationRequest(homeDirectory, transferFile, modelNames);
            var command = ilitoolsExecutor.CreateIlivalidatorCommand(request);

            StringAssert.Contains(command, $"--log \"{request.LogFilePath}\"");
            StringAssert.Contains(command, $"--xtflog \"{request.XtfLogFilePath}\"");
            StringAssert.Contains(command, $"\"{request.FilePath}\"");

            // Model names should not be included in ilivalidator command
            StringAssert.DoesNotMatch(command, new Regex("--models"));
        }

        private ValidationRequest CreateValidationRequest(
            string homeDirectory,
            string transferFile,
            string profileId,
            string modelNames = null,
            List<string> additionalCatalogueFilePaths = null,
            bool verboseLogging = true,
            bool log = true,
            bool xtfLog = true)
        {
            var transferFileNameWithoutExtension = Path.GetFileNameWithoutExtension(transferFile);
            var logPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.log");
            var xtfLogPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.xtf");
            var csvLogPath = Path.Combine(homeDirectory, $"{transferFileNameWithoutExtension}_log.csv");
            var transferFilePath = Path.Combine(homeDirectory, transferFile);

            return new ValidationRequest
            {
                FileName = transferFile,
                FilePath = transferFilePath,
                LogFilePath = log ? logPath : null,
                XtfLogFilePath = xtfLog ? xtfLogPath : null,
                CsvLogFilePath = csvLogPath,
                GpkgModelNames = modelNames,
                VerboseLogging = verboseLogging,
                AdditionalCatalogueFilePaths = additionalCatalogueFilePaths ?? new List<string>(),
                Profile = new Profile { Id = profileId },
            };
        }
    }
}
