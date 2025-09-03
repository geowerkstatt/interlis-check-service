using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Interlis.RepositoryCrawler;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Services;

/// <inheritdoc cref="IProfileService" />
public class ProfileService : IProfileService
{
    private readonly RepositoryReader repositoryReader;

    public ProfileService(RepositoryReader repositoryReader)
    {
        this.repositoryReader = repositoryReader;
    }

    /// <inheritdoc />
    public async Task<List<Profile>> GetProfiles()
    {
        var iliData = await repositoryReader.ReadIliData();

        return iliData
            .Where(d => d.IsMetaconfig())
            .Select(d => new Profile
            {
                Id = d.id,
                Titles = d.title.MultilingualText.LocalisedTexts.Select(t => new LocalisedText { Language = t.Language, Text = t.Text }).ToList(),
            }).ToList();
    }
}
