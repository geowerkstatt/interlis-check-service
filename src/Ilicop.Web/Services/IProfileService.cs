using Geowerkstatt.Ilicop.Web.Contracts;
using Geowerkstatt.Interlis.RepositoryCrawler;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web.Services
{
    /// <summary>
    /// Service for getting INTERLIS validation profiles.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Returns all available profiles.
        /// </summary>
        /// <exception cref="RepositoryReaderException">If the profiles could not be loaded from the configured repository.</exception>
        Task<List<Profile>> GetProfiles();
    }
}
