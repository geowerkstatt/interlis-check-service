using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Services;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Controllers
{
    [TestClass]
    public sealed class UploadControllerTest
    {
        private readonly string jobId = "28e1adff-765e-4c0b-b667-90458b33e1ca";

        private Mock<ILogger<UploadController>> loggerMock;
        private Mock<IHttpContextAccessor> httpContextAccessorMock;
        private Mock<IValidator> validatorMock;
        private Mock<PhysicalFileProvider> fileProviderMock;
        private Mock<ApiVersion> apiVersionMock;
        private Mock<IFormFile> formFileMock;
        private Mock<IValidatorService> validatorServiceMock;
        private Mock<IProfileService> profileServiceMock;
        private UploadController controller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            loggerMock = new Mock<ILogger<UploadController>>();
            httpContextAccessorMock = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            validatorMock = new Mock<IValidator>(MockBehavior.Strict);
            fileProviderMock = new Mock<PhysicalFileProvider>(MockBehavior.Strict, CreateConfiguration(), "ILICOP_UPLOADS_DIR");
            validatorServiceMock = new Mock<IValidatorService>(MockBehavior.Strict);
            profileServiceMock = new Mock<IProfileService>(MockBehavior.Strict);
            formFileMock = new Mock<IFormFile>(MockBehavior.Strict);
            apiVersionMock = new Mock<ApiVersion>(MockBehavior.Strict, 9, 88);

            validatorMock.SetupGet(x => x.Id).Returns(new Guid(jobId));

            controller = new UploadController(
                loggerMock.Object,
                CreateConfiguration(),
                httpContextAccessorMock.Object,
                validatorMock.Object,
                fileProviderMock.Object,
                validatorServiceMock.Object,
                profileServiceMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            loggerMock.VerifyAll();
            httpContextAccessorMock.VerifyAll();
            validatorMock.VerifyAll();
            fileProviderMock.VerifyAll();
            formFileMock.VerifyAll();
            validatorServiceMock.VerifyAll();
            profileServiceMock.VerifyAll();
            apiVersionMock.VerifyAll();

            controller.Dispose();
        }

        [TestMethod]
        public async Task UploadAsync()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            formFileMock.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            validatorServiceMock.Setup(x => x.EnqueueJobAsync(
                It.Is<Guid>(x => x.Equals(new Guid(jobId))),
                It.IsAny<Func<CancellationToken, Task>>())).Returns(Task.FromResult(0));
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, "DEFAULT") as CreatedResult;

            Assert.IsInstanceOfType(response, typeof(CreatedResult));
            Assert.IsInstanceOfType(response.Value, typeof(UploadResponse));
            Assert.AreEqual(StatusCodes.Status201Created, response.StatusCode);
            Assert.AreEqual($"/api/v9/status/{jobId}", response.Location);
            Assert.AreEqual(jobId, ((UploadResponse)response.Value).JobId.ToString());
            Assert.AreEqual($"/api/v9/status/{jobId}", ((UploadResponse)response.Value).StatusUrl.ToString());
        }

        [TestMethod]
        public async Task UploadAsyncWithProfileNullAndWithDefaultProfileExisting()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            formFileMock.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            validatorServiceMock.Setup(x => x.EnqueueJobAsync(
                It.Is<Guid>(x => x.Equals(new Guid(jobId))),
                It.IsAny<Func<CancellationToken, Task>>())).Returns(Task.FromResult(0));
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, null) as CreatedResult;

            Assert.IsInstanceOfType(response, typeof(CreatedResult));
            Assert.IsInstanceOfType(response.Value, typeof(UploadResponse));
            Assert.AreEqual(StatusCodes.Status201Created, response.StatusCode);
        }

        [TestMethod]
        public async Task UploadAsyncWithProfileNullAndWithDefaultProfileNotExisting()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(new List<Profile> { new Profile { Id = "test-profile" } });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, null) as ObjectResult;
            var problemDetails = response?.Value as ProblemDetails;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.IsInstanceOfType(problemDetails, typeof(ProblemDetails));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            Assert.AreEqual("There is no default profile. A valid profile must be explicitly specified.", problemDetails.Detail);
        }

        [TestMethod]
        public async Task UploadAsyncWithNonExistentProfile()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(
                new List<Profile>
                {
                    new Profile { Id = "DEFAULT" },
                    new Profile { Id = "test-profile" },
                });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, "non-existent-profile") as ObjectResult;
            var problemDetails = response?.Value as ProblemDetails;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.IsInstanceOfType(problemDetails, typeof(ProblemDetails));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            Assert.AreEqual("The specified profile <non-existent-profile> does not exist.", problemDetails.Detail);
        }

        [TestMethod]
        public async Task UploadAsyncWithEmptyStringAsProfile()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(
                new List<Profile>
                {
                    new Profile { Id = "DEFAULT" },
                    new Profile { Id = "test-profile" },
                });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, "") as ObjectResult;
            var problemDetails = response?.Value as ProblemDetails;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.IsInstanceOfType(problemDetails, typeof(ProblemDetails));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            Assert.AreEqual("The specified profile <> does not exist.", problemDetails.Detail);
        }

        [TestMethod]
        public async Task UploadAsyncHandlesRepositoryReaderException()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("BIZARRESCAN.xtf");
            profileServiceMock.Setup(x => x.GetProfiles()).ThrowsAsync(new RepositoryReaderException());

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, "DEFAULT") as ObjectResult;
            var problemDetails = response?.Value as ProblemDetails;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.IsInstanceOfType(problemDetails, typeof(ProblemDetails));
            Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
            Assert.AreEqual("Error retrieving profiles.", problemDetails.Detail);
        }

        [TestMethod]
        public async Task UploadAsyncForFileNull()
        {
            var response = await controller.UploadAsync(apiVersionMock.Object, null, null) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            Assert.AreEqual("Form data <file> cannot be empty.", ((ProblemDetails)response.Value).Detail);
        }

        [TestMethod]
        public async Task UploadAsyncForFileInvalid()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentLength = 1234;
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            formFileMock.SetupGet(x => x.FileName).Returns("SPATULASET.cmd");
            profileServiceMock.Setup(x => x.GetProfiles()).ReturnsAsync(new List<Profile> { new Profile { Id = "DEFAULT" } });

            var response = await controller.UploadAsync(apiVersionMock.Object, formFileMock.Object, null) as ObjectResult;

            Assert.IsInstanceOfType(response, typeof(ObjectResult));
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            Assert.AreEqual("Transfer file extension <.cmd> is an unknown file extension.", ((ProblemDetails)response.Value).Detail);
        }

        private IConfiguration CreateConfiguration(bool enableGpkgValidation = false) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ILICOP_UPLOADS_DIR", TestContext.DeploymentDirectory },
                { "ENABLE_GPKG_VALIDATION", enableGpkgValidation.ToString() },
            }).Build();
    }
}
