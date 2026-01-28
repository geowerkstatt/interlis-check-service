using ILICheck.Web.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ILICheck.Web
{
    [TestClass]
    public class ValidatorTest
    {
        private readonly string jobId = "2b005f1a-4eac-4d05-8ac6-c9221250f5a0";

        private JsonOptions jsonOptions;
        private Mock<ILogger<Validator>> loggerMock;
        private Mock<PhysicalFileProvider> fileProviderMock;
        private Mock<Validator> validatorMock;
        private Mock<IlitoolsExecutor> ilitoolsExecutorMock;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            var configuration = CreateConfiguration();
            var ilitoolsEnvironment = new IlitoolsEnvironment
            {
                InstallationDir = Path.Combine(TestContext.DeploymentDirectory, "FALLOUT"),
                CacheDir = Path.Combine(TestContext.DeploymentDirectory, "ARKSHARK"),
                EnableGpkgValidation = false,
            };

            jsonOptions = new JsonOptions();
            loggerMock = new Mock<ILogger<Validator>>();
            fileProviderMock = new Mock<PhysicalFileProvider>(MockBehavior.Strict, configuration, "ILICHECK_UPLOADS_DIR");
            ilitoolsExecutorMock = new Mock<IlitoolsExecutor>(MockBehavior.Strict, new Mock<ILogger<IlitoolsExecutor>>().Object, ilitoolsEnvironment, configuration);

            validatorMock = new Mock<Validator>(MockBehavior.Strict, loggerMock.Object, configuration, fileProviderMock.Object, Options.Create(jsonOptions), ilitoolsExecutorMock.Object);

            validatorMock.SetupGet(x => x.Id).Returns(new Guid(jobId));
        }

        [TestCleanup]
        public void Cleanup()
        {
            loggerMock.VerifyAll();
            fileProviderMock.VerifyAll();
            validatorMock.VerifyAll();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException), "Null argument should be rejected.")]
        public async Task ValidateXmlAsyncForNull()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns((string)null);
            await validatorMock.Object.ValidateXmlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Empty argument should be rejected.")]
        public async Task ValidateXmlAsyncForEmpty()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns(string.Empty);
            await validatorMock.Object.ValidateXmlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [DeploymentItem(@"testdata/invalid.xtf", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        [ExpectedException(typeof(InvalidXmlException), "Corrupt or invalid transfer file should be rejected.")]
        public async Task ValidateXmlAsyncForInvalid()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("invalid.xtf");
            fileProviderMock.Setup(x => x.OpenText("invalid.xtf")).CallBase();
            await validatorMock.Object.ValidateXmlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException), "Not existing file path should be detected.")]
        public async Task ValidateXmlAsyncForFileNotFound()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("unavailable.xtf");
            fileProviderMock.Setup(x => x.OpenText("unavailable.xtf")).CallBase();
            await validatorMock.Object.ValidateXmlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [DeploymentItem("testdata/example.xtf", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        public async Task ValidateXmlAsync()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("example.xtf");
            fileProviderMock.Setup(x => x.OpenText("example.xtf")).CallBase();
            await validatorMock.Object.ValidateXmlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException), "Null argument should be rejected.")]
        public async Task ReadGpkgModelNamesAsyncForNull()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns((string)null);
            await validatorMock.Object.ReadGpkgModelNamesAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Empty argument should be rejected.")]
        public async Task ReadGpkgModelNamesAsyncForEmpty()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns(string.Empty);
            await validatorMock.Object.ReadGpkgModelNamesAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [ExpectedException(typeof(GeoPackageException), "Corrupt or invalid GeoPackage should be rejected.")]
        public async Task ReadGpkgModelNamesAsyncForInvalid()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("invalid.gpkg");
            await validatorMock.Object.ReadGpkgModelNamesAsync().ConfigureAwait(false);
        }

        [TestMethod]
        [DeploymentItem(@"testdata/example.gpkg", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        public async Task ReadGpkgModelNamesAsync()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("example.gpkg");
            var expected = "Wildruhezonen_Codelisten_V2_1;Wildruhezonen_LV03_V2_1;Wildruhezonen_LV95_V2_1;LOUDTRINITY";

            Assert.AreEqual(expected, await validatorMock.Object.ReadGpkgModelNamesAsync().ConfigureAwait(false));
        }

        [TestMethod]
        public async Task CleanUploadDirectoryAsync()
        {
            validatorMock.SetupGet(x => x.TransferFile).Returns("example.xtf");
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "example.ili", "example.xtf", "example_log.xtf" });
            fileProviderMock.Setup(x => x.DeleteFileAsync(It.Is<string>(x => x == "example.xtf"))).Returns(Task.FromResult(0));
            fileProviderMock.Setup(x => x.DeleteFileAsync(It.Is<string>(x => x == "example.ili"))).Returns(Task.FromResult(0));

            await validatorMock.Object.CleanUploadDirectoryAsync().ConfigureAwait(false);
        }

        private IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "DELETE_TRANSFER_FILES", "TRUE" },
                { "ILICHECK_UPLOADS_DIR", TestContext.DeploymentDirectory },
                { "Validation:BlacklistedGpkgModels", "PEEVEDSCAN;WRONGPENGUIN;SPORKDEITY;CHBaseEx_MapCatalogue_V1;CHBaseEx_WaterNet_V1;CHBaseEx_Sewage_V1;CHAdminCodes_V1;AdministrativeUnits_V1;AdministrativeUnitsCH_V1;WithOneState_V1;WithLatestModification_V1;WithModificationObjects_V1;GraphicCHLV03_V1;GraphicCHLV95_V1;NonVector_Base_V2;NonVector_Base_V3;NonVector_Base_LV03_V3_1;NonVector_Base_LV95_V3_1;GeometryCHLV03_V1;GeometryCHLV95_V1;InternationalCodes_V1;Localisation_V1;LocalisationCH_V1;Dictionaries_V1;DictionariesCH_V1;CatalogueObjects_V1;CatalogueObjectTrees_V1;AbstractSymbology;CodeISO;CoordSys;GM03_2_1Comprehensive;GM03_2_1Core;GM03_2Comprehensive;GM03_2Core;GM03Comprehensive;GM03Core;IliRepository09;IliSite09;IlisMeta07;IliVErrors;INTERLIS_ext;RoadsExdm2ben;RoadsExdm2ben_10;RoadsExgm2ien;RoadsExgm2ien_10;StandardSymbology;StandardSymbology;Time;Units" },
            }).Build();
    }
}
