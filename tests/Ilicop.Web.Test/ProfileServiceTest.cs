using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Services;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.XmlModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Geowerkstatt.Ilicop.TestHelpers.ProfileTestHelper;

namespace Geowerkstatt.Ilicop.Web;

[TestClass]
public sealed class ProfileServiceTest
{
    private Mock<RepositoryReader> readerMock;
    private ProfileService service;

    [TestInitialize]
    public void Initialize()
    {
        readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        service = new ProfileService(readerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        readerMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetProfiles()
    {
        var iliData = new List<DatasetMetadata>();
        var expectedProfiles = new List<Profile>();

        iliData.Add(CreateDatasetMetadata("DEFAULT", [(null, "")], ["http://codes.interlis.ch/type/metaconfig"]));
        expectedProfiles.Add(CreateProfile("DEFAULT", [(null, "")]));

        iliData.Add(CreateDatasetMetadata("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")], ["http://codes.interlis.ch/type/metaconfig"]));
        expectedProfiles.Add(CreateProfile("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")]));

        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);

        var actualProfiles = await service.GetProfiles();

        Assert.HasCount(expectedProfiles.Count, actualProfiles);

        Assert.AreEqual(expectedProfiles[0].Id, actualProfiles[0].Id);
        Assert.HasCount(expectedProfiles[0].Titles.Count, actualProfiles[0].Titles);
        Assert.AreEqual(expectedProfiles[0].Titles[0].Language, actualProfiles[0].Titles[0].Language);
        Assert.AreEqual(expectedProfiles[0].Titles[0].Text, actualProfiles[0].Titles[0].Text);

        Assert.AreEqual(expectedProfiles[1].Id, actualProfiles[1].Id);
        Assert.HasCount(expectedProfiles[1].Titles.Count, actualProfiles[1].Titles);
        Assert.AreEqual(expectedProfiles[1].Titles[0].Language, actualProfiles[1].Titles[0].Language);
        Assert.AreEqual(expectedProfiles[1].Titles[0].Text, actualProfiles[1].Titles[0].Text);
        Assert.AreEqual(expectedProfiles[1].Titles[1].Language, actualProfiles[1].Titles[1].Language);
        Assert.AreEqual(expectedProfiles[1].Titles[1].Text, actualProfiles[1].Titles[1].Text);
    }

    [TestMethod]
    public async Task GetProfilesFilteresByMetaconfigCategory()
    {
        var iliData = new List<DatasetMetadata>();

        iliData.Add(CreateDatasetMetadata("DEFAULT", [(null, "")], ["http://codes.interlis.ch/type/metaconfig"]));
        var profile1 = CreateProfile("DEFAULT", [(null, "")]);

        // This one is not a metaconfig profile and should be filtered out
        iliData.Add(CreateDatasetMetadata("not-metaconfig", [("de", "Nicht Metaconfig"), ("en", "Not Metaconfig")]));

        iliData.Add(CreateDatasetMetadata("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")], ["http://codes.interlis.ch/type/metaconfig"]));
        var profile2 = CreateProfile("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")]);

        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);

        var actualProfiles = await service.GetProfiles();

        Assert.HasCount(2, actualProfiles);
        Assert.AreEqual(profile1.Id, actualProfiles[0].Id);
        Assert.AreEqual(profile2.Id, actualProfiles[1].Id);
    }

    [TestMethod]
    public async Task GetProfilesThrowsError()
    {
        readerMock.Setup(x => x.ReadIliData()).ThrowsAsync(new RepositoryReaderException("Could not read ili data"));

        await Assert.ThrowsExactlyAsync<RepositoryReaderException>(service.GetProfiles);
    }

    [TestMethod]
    public async Task GetProfilesEmpty()
    {
        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(new List<DatasetMetadata>());

        var profiles = await service.GetProfiles();

        Assert.IsNotNull(profiles);
        Assert.IsEmpty(profiles);
    }

    [TestMethod]
    public async Task GetProfilesNoMetaconfig()
    {
        var iliData = new List<DatasetMetadata>()
        {
            CreateDatasetMetadata("not-metaconfig-1", [("de", "Nicht Metaconfig 1"), ("en", "Not Metaconfig 1")]),
            CreateDatasetMetadata("not-metaconfig-2", [("de", "Nicht Metaconfig 2"), ("en", "Not Metaconfig 2")]),
        };

        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);

        var profiles = await service.GetProfiles();

        Assert.IsNotNull(profiles);
        Assert.IsEmpty(profiles);
    }
}
