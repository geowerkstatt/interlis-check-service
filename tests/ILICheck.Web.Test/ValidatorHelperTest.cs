using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using static ILICheck.Web.ValidatorHelper;

namespace ILICheck.Web
{
    [TestClass]
    public class ValidatorHelperTest
    {
        [TestMethod]
        public void GetAcceptedFileExtensionsForUserUploadsWithoutGpkgEnabled()
        {
            var expected = new[] { ".xtf", ".itf", ".xml", ".zip" };
            var configuration = CreateConfiguration(enableGpkgValidation: false);
            CollectionAssert.AreEqual(expected, GetAcceptedFileExtensionsForUserUploads(configuration).ToList());
        }

        [TestMethod]
        public void GetAcceptedFileExtensionsForUserUploadsWithGpkgEnabled()
        {
            var expected = new[] { ".xtf", ".itf", ".xml", ".gpkg", ".zip" };
            var configuration = CreateConfiguration(enableGpkgValidation: true);
            CollectionAssert.AreEqual(expected, GetAcceptedFileExtensionsForUserUploads(configuration).ToList());
        }

        [TestMethod]
        public void GetAcceptedFileExtensionsForZipContentWithoutGpkgEnabled()
        {
            var expected = new[] { ".xtf", ".itf", ".xml", ".ili" };
            var configuration = CreateConfiguration(enableGpkgValidation: false);
            CollectionAssert.AreEqual(expected, GetAcceptedFileExtensionsForZipContent(configuration).ToList());
        }

        [TestMethod]
        public void GetAcceptedFileExtensionsForZipContentWithGpkgEnabled()
        {
            var expected = new[] { ".xtf", ".itf", ".xml", ".gpkg", ".ili" };
            var configuration = CreateConfiguration(enableGpkgValidation: true);
            CollectionAssert.AreEqual(expected, GetAcceptedFileExtensionsForZipContent(configuration).ToList());
        }

        private static IConfiguration CreateConfiguration(bool enableGpkgValidation = false, string commandFormat = "") =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ENABLE_GPKG_VALIDATION", enableGpkgValidation.ToString() },
                { "Validation:CommandFormat", commandFormat },
            }).Build();
    }
}
