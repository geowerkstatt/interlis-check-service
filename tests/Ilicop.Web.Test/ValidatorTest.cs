using Geowerkstatt.Ilicop.Web.Ilitools;
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

namespace Geowerkstatt.Ilicop.Web
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
            fileProviderMock = new Mock<PhysicalFileProvider>(MockBehavior.Strict, configuration, "ILICOP_UPLOADS_DIR");
            ilitoolsExecutorMock = new Mock<IlitoolsExecutor>(MockBehavior.Strict, new Mock<ILogger<IlitoolsExecutor>>().Object, ilitoolsEnvironment, configuration);

            validatorMock = new Mock<Validator>(MockBehavior.Strict, loggerMock.Object, CreateConfiguration(), fileProviderMock.Object, Options.Create(jsonOptions), ilitoolsExecutorMock.Object);

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
        public async Task ValidateXmlAsyncForNull()
        {
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await validatorMock.Object.ValidateXmlAsync(null).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task ValidateXmlAsyncForEmpty()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await validatorMock.Object.ValidateXmlAsync(string.Empty).ConfigureAwait(false));
        }

        [TestMethod]
        [DeploymentItem(@"testdata/invalid.xtf", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        public async Task ValidateXmlAsyncForInvalid()
        {
            fileProviderMock.Setup(x => x.OpenText("invalid.xtf")).CallBase();
            await Assert.ThrowsExactlyAsync<InvalidXmlException>(async () => await validatorMock.Object.ValidateXmlAsync("invalid.xtf").ConfigureAwait(false));
        }

        [TestMethod]
        public async Task ValidateXmlAsyncForFileNotFound()
        {
            fileProviderMock.Setup(x => x.OpenText("unavailable.xtf")).CallBase();
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () => await validatorMock.Object.ValidateXmlAsync("unavailable.xtf").ConfigureAwait(false));
        }

        [TestMethod]
        [DeploymentItem("testdata/example.xtf", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        public async Task ValidateXmlAsync()
        {
            var transferFile = "example.xtf";
            fileProviderMock.Setup(x => x.OpenText(transferFile)).CallBase();
            await validatorMock.Object.ValidateXmlAsync(transferFile).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ReadGpkgModelNamesAsyncForNull()
        {
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await validatorMock.Object.ReadGpkgModelNamesAsync(null).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task ReadGpkgModelNamesAsyncForEmpty()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await validatorMock.Object.ReadGpkgModelNamesAsync(string.Empty).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task ReadGpkgModelNamesAsyncForInvalid()
        {
            await Assert.ThrowsExactlyAsync<GeoPackageException>(async () => await validatorMock.Object.ReadGpkgModelNamesAsync("invalid.gpkg").ConfigureAwait(false));
        }

        [TestMethod]
        [DeploymentItem(@"testdata/example.gpkg", "2b005f1a-4eac-4d05-8ac6-c9221250f5a0")]
        public async Task ReadGpkgModelNamesAsync()
        {
            var expected = "Wildruhezonen_Codelisten_V2_1;Wildruhezonen_LV03_V2_1;Wildruhezonen_LV95_V2_1;LOUDTRINITY";

            Assert.AreEqual(expected, await validatorMock.Object.ReadGpkgModelNamesAsync("example.gpkg").ConfigureAwait(false));
        }

        [TestMethod]
        public async Task CleanUploadDirectoryAsync()
        {
            var transferFile = "example.xtf";
            fileProviderMock.Setup(x => x.GetFiles()).Returns(new[] { "example.ili", transferFile, "example_log.xtf" });
            fileProviderMock.Setup(x => x.DeleteFileAsync(It.Is<string>(x => x == transferFile))).Returns(Task.FromResult(0));
            fileProviderMock.Setup(x => x.DeleteFileAsync(It.Is<string>(x => x == "example.ili"))).Returns(Task.FromResult(0));

            await validatorMock.Object.CleanUploadDirectoryAsync(transferFile).ConfigureAwait(false);
        }

        private IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "DELETE_TRANSFER_FILES", "TRUE" },
                { "ILICOP_UPLOADS_DIR", TestContext.DeploymentDirectory },
                { "Validation:BlacklistedGpkgModels", "PEEVEDSCAN;WRONGPENGUIN;SPORKDEITY;CHBaseEx_MapCatalogue_V1;CHBaseEx_WaterNet_V1;CHBaseEx_Sewage_V1;CHAdminCodes_V1;AdministrativeUnits_V1;AdministrativeUnitsCH_V1;WithOneState_V1;WithLatestModification_V1;WithModificationObjects_V1;GraphicCHLV03_V1;GraphicCHLV95_V1;NonVector_Base_V2;NonVector_Base_V3;NonVector_Base_LV03_V3_1;NonVector_Base_LV95_V3_1;GeometryCHLV03_V1;GeometryCHLV95_V1;InternationalCodes_V1;Localisation_V1;LocalisationCH_V1;Dictionaries_V1;DictionariesCH_V1;CatalogueObjects_V1;CatalogueObjectTrees_V1;AbstractSymbology;CodeISO;CoordSys;GM03_2_1Comprehensive;GM03_2_1Core;GM03_2Comprehensive;GM03_2Core;GM03Comprehensive;GM03Core;IliRepository09;IliSite09;IlisMeta07;IliVErrors;INTERLIS_ext;RoadsExdm2ben;RoadsExdm2ben_10;RoadsExgm2ien;RoadsExgm2ien_10;StandardSymbology;StandardSymbology;Time;Units" },
            }).Build();
    }
}
