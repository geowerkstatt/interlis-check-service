using Geowerkstatt.Ilicop.Web.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Schedules validation jobs and provides access to status information for a specific job.
    /// </summary>
    public class ValidatorService : BackgroundService, IValidatorService
    {
        private readonly ILogger<ValidatorService> logger;
        private readonly Channel<(Guid Id, Func<CancellationToken, Task> Task)> queue;
        private readonly ConcurrentDictionary<Guid, (Status Status, string StatusMessage)> jobs = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatorService"/> class.
        /// </summary>
        public ValidatorService(ILogger<ValidatorService> logger)
        {
            this.logger = logger;
            queue = Channel.CreateUnbounded<(Guid, Func<CancellationToken, Task>)>();
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Parallel.ForEachAsync(queue.Reader.ReadAllAsync(stoppingToken), stoppingToken, async (item, stoppingToken) =>
            {
                try
                {
                    UpdateJobStatus(item.Id, Status.Processing, "Validating file");
                    await item.Task(stoppingToken);
                    UpdateJobStatus(item.Id, Status.Completed, "Validation successful");
                }
                catch (UnknownExtensionException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "File extension not allowed", ex.Message);
                }
                catch (MultipleTransferFileFoundException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "Multiple transfer files found", ex.Message);
                }
                catch (TransferFileNotFoundException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "No transfer file found", ex.Message);
                }
                catch (GeoPackageException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "Could not read model names from GeoPackage", ex.Message);
                }
                catch (InvalidXmlException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "Invalid XML structure", ex.Message);
                }
                catch (ValidationFailedException ex)
                {
                    UpdateJobStatus(item.Id, Status.CompletedWithErrors, "Data not conform to INTERLIS model", ex.Message);
                }
                catch (Exception ex)
                {
                    var traceId = Guid.NewGuid();
                    UpdateJobStatus(item.Id, Status.Failed, $"Unknown error. Error ID: <{traceId}>");
                    logger.LogError(ex, "Unhandled exception TraceId: <{TraceId}> Message: <{ErrorMessage}>", traceId, ex.Message);
                }
            });
        }

        /// <inheritdoc/>
        public async Task EnqueueJobAsync(Guid jobId, Func<CancellationToken, Task> action)
        {
            UpdateJobStatus(jobId, Status.Enqueued, "Preparing validation");
            await queue.Writer.WriteAsync((jobId, action));
        }

        /// <inheritdoc/>
        public (Status Status, string StatusMessage) GetJobStatusOrDefault(Guid jobId) =>
            jobs.TryGetValue(jobId, out var status) ? status : default;

        /// <summary>
        /// Adds or updates the status for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier to be added or whose value should be updated.</param>
        /// <param name="status">The status.</param>
        /// <param name="statusMessage">The status message.</param>
        /// <param name="logMessage">Optional info log message.</param>
        private void UpdateJobStatus(Guid jobId, Status status, string statusMessage, string logMessage = null)
        {
            jobs[jobId] = (status, statusMessage);
            if (!string.IsNullOrEmpty(logMessage)) logger.LogInformation(logMessage);
        }
    }
}
