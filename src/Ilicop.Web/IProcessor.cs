using Geowerkstatt.Ilicop.Web.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

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
    /// <param name="transferFile">The transfer file to be processed.</param>
    /// <param name="profile">The profile with which the processing should be done.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    Task Run(Guid jobId, string transferFile, Profile profile, CancellationToken cancellationToken);
}
