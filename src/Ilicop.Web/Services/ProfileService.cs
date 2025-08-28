using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Ilicop.Web.Ilitools;
using Geowerkstatt.Interlis.RepositoryCrawler;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Services;

/// <inheritdoc cref="IProfileService" />
public class ProfileService : IProfileService
{
    private readonly IlitoolsEnvironment ilitoolsEnvironment;
    private readonly HttpClient httpClient;

    public ProfileService(IlitoolsEnvironment ilitoolsEnvironment, HttpClient httpClient)
    {
        this.ilitoolsEnvironment = ilitoolsEnvironment;
        this.httpClient = httpClient;
    }

    /// <inheritdoc />
    /// <exception cref="RepositoryReaderException">If the profiles could not be loaded from the configured repository.</exception>
    public async Task<List<Profile>> GetProfiles()
    {
        var repositoryReader = RepositoryReaderFactory.Create(ilitoolsEnvironment.ModelRepositoryDir, httpClient);
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
