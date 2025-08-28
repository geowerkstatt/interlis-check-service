using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Services;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.XmlModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop;

[TestClass]
public sealed class ProfileServiceTest
{
    [TestMethod]
    public async Task GetProfiles()
    {
        var iliData = new List<DatasetMetadata>();
        var expectedProfiles = new List<Profile>();

        iliData.Add(CreateDatasetMetadata("DEFAULT", [(null, "")], ["http://codes.interlis.ch/type/metaconfig"]));
        expectedProfiles.Add(CreateProfile("DEFAULT", [(null, "")]));

        iliData.Add(CreateDatasetMetadata("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")], ["http://codes.interlis.ch/type/metaconfig"]));
        expectedProfiles.Add(CreateProfile("test-profile-0", [("de", "Testprofil 0"), ("en", "Test profile 0")]));

        var readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);
        var service = new ProfileService(readerMock.Object);

        var actualProfiles = await service.GetProfiles();

        Assert.AreEqual(expectedProfiles.Count, actualProfiles.Count);

        Assert.AreEqual(expectedProfiles[0].Id, actualProfiles[0].Id);
        Assert.AreEqual(expectedProfiles[0].Titles.Count, actualProfiles[0].Titles.Count);
        Assert.AreEqual(expectedProfiles[0].Titles[0].Language, actualProfiles[0].Titles[0].Language);
        Assert.AreEqual(expectedProfiles[0].Titles[0].Text, actualProfiles[0].Titles[0].Text);

        Assert.AreEqual(expectedProfiles[1].Id, actualProfiles[1].Id);
        Assert.AreEqual(expectedProfiles[1].Titles.Count, actualProfiles[1].Titles.Count);
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

        var readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);
        var service = new ProfileService(readerMock.Object);

        var actualProfiles = await service.GetProfiles();

        Assert.AreEqual(2, actualProfiles.Count);
        Assert.AreEqual(profile1.Id, actualProfiles[0].Id);
        Assert.AreEqual(profile2.Id, actualProfiles[1].Id);
    }

    [TestMethod]
    public async Task GetProfilesThrowsError()
    {
        var readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        readerMock.Setup(x => x.ReadIliData()).ThrowsAsync(new RepositoryReaderException("Could not read ili data"));
        var service = new ProfileService(readerMock.Object);

        await Assert.ThrowsExactlyAsync<RepositoryReaderException>(service.GetProfiles);
    }

    [TestMethod]
    public async Task GetProfilesEmpty()
    {
        var readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(new List<DatasetMetadata>());
        var service = new ProfileService(readerMock.Object);

        var profiles = await service.GetProfiles();

        Assert.IsNotNull(profiles);
        Assert.AreEqual(0, profiles.Count);
    }

    [TestMethod]
    public async Task GetProfilesNoMetaconfig()
    {
        var iliData = new List<DatasetMetadata>()
        {
            CreateDatasetMetadata("not-metaconfig-1", [("de", "Nicht Metaconfig 1"), ("en", "Not Metaconfig 1")]),
            CreateDatasetMetadata("not-metaconfig-2", [("de", "Nicht Metaconfig 2"), ("en", "Not Metaconfig 2")]),
        };

        var readerMock = new Mock<RepositoryReader>(MockBehavior.Strict);
        readerMock.Setup(x => x.ReadIliData()).ReturnsAsync(iliData);
        var service = new ProfileService(readerMock.Object);

        var profiles = await service.GetProfiles();

        Assert.IsNotNull(profiles);
        Assert.AreEqual(0, profiles.Count);
    }

    private static DatasetMetadata CreateDatasetMetadata(
        string id,
        (string Language, string Text)[] titles,
        string[] categories = null)
    {
        return new DatasetMetadata
        {
            id = id,
            title = new Title
            {
                MultilingualText = new MultilingualText
                {
                    LocalisedTexts = titles
                            .Select(t => new Interlis.RepositoryCrawler.XmlModels.LocalisedText { Language = t.Language, Text = t.Text })
                            .ToArray(),
                },
            },
            categories = categories?.Select(c => new CategoryCodesCode { value = c }).ToArray(),
        };
    }

    private static Profile CreateProfile(
        string id,
        (string Language, string Text)[] titles)
    {
        return new Profile
        {
            Id = id,
            Titles = titles
                    .Select(t => new Web.Contracts.LocalisedText { Language = t.Language, Text = t.Text })
                    .ToList(),
        };
    }
}
