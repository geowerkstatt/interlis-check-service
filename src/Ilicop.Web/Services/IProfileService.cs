using Geowerkstatt.Ilicop.Web.Contracts;
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
        Task<List<Profile>> GetProfiles();
    }
}
