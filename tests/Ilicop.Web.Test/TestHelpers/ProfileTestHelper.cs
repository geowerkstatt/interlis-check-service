using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Interlis.RepositoryCrawler.XmlModels;
using System.Linq;

namespace Geowerkstatt.Ilicop.TestHelpers;

public static class ProfileTestHelper
{
    public static DatasetMetadata CreateDatasetMetadata(
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

    public static Profile CreateProfile(
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
