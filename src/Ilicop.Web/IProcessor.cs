using Geowerkstatt.Ilicop.Web.Contracts;
using System;

namespace Geowerkstatt.Ilicop.Web;

/// <summary>
/// Does further processing after validation.
/// </summary>
public interface IProcessor
{
    /// <summary>
    /// Runs the processor for the specified job and profile.
    /// </summary>
    /// <param name="jobId">The id of the job.</param>
    /// <param name="profile">The profile with which the processing should be done.</param>
    void Run(Guid jobId, Profile profile);
}
