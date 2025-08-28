using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Controllers;
using Geowerkstatt.Ilicop.Web.Services;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Geowerkstatt.Ilicop.TestHelpers.ProfileTestHelper;

namespace Geowerkstatt.Ilicop.Web;

[TestClass]
public sealed class ProfileControllerTest
{
    private Mock<ILogger<ProfileController>> loggerMock;
    private Mock<IProfileService> profileServiceMock;
    private ProfileController controller;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<ProfileController>>();
        profileServiceMock = new Mock<IProfileService>(MockBehavior.Strict);
        controller = new ProfileController(loggerMock.Object, profileServiceMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        profileServiceMock.VerifyAll();
        controller.Dispose();
    }

    [TestMethod]
    public async Task GetProfiles()
    {
        var profiles = new List<Profile>
        {
            CreateProfile("DEFAULT", [(null, "")]),
            CreateProfile("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")]),
            CreateProfile("test-profile-1", [("en", "Test profile 1")]),
        };

        profileServiceMock
            .Setup(s => s.GetProfiles())
            .ReturnsAsync(profiles);

        var response = await controller.GetAll() as OkObjectResult;

        Assert.IsInstanceOfType(response, typeof(OkObjectResult));
        Assert.IsInstanceOfType(response.Value, typeof(IEnumerable<Profile>));
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        CollectionAssert.AreEqual(profiles, (List<Profile>)response.Value);
    }

    [TestMethod]
    public async Task GetProfilesEmptyList()
    {
        profileServiceMock
            .Setup(s => s.GetProfiles())
            .ReturnsAsync(new List<Profile>());

        var response = await controller.GetAll() as ObjectResult;

        Assert.IsInstanceOfType(response, typeof(OkObjectResult));
        Assert.IsInstanceOfType(response.Value, typeof(List<Profile>));
        Assert.AreEqual(0, ((List<Profile>)response.Value).Count);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetProfilesHandlesRepositoryError()
    {
        profileServiceMock
            .Setup(s => s.GetProfiles())
            .ThrowsAsync(new RepositoryReaderException("Could not read repository"));

        var response = await controller.GetAll() as ObjectResult;
        var problemDetails = response?.Value as ProblemDetails;

        Assert.IsInstanceOfType(response, typeof(ObjectResult));
        Assert.IsInstanceOfType(problemDetails, typeof(ProblemDetails));
        Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.AreEqual("Internal Server Error", problemDetails.Title);
        Assert.AreEqual("Error while loading profiles", problemDetails.Detail);
    }
}
