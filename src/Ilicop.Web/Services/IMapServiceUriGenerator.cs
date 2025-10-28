using System;

namespace Geowerkstatt.Ilicop.Web.Services
{
    public interface IMapServiceUriGenerator
    {
        /// <summary>
        /// Builds the map service URI for the given job ID.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns></returns>
        public Uri BuildMapServiceUri(Guid jobId);
    }
}
