using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// ジョブステータス更新Activity
/// </summary>
public class UpdateJobStatusActivity
{
    private readonly ILogger<UpdateJobStatusActivity> _logger;
    private readonly IJobRepository _jobRepository;

    public UpdateJobStatusActivity(
        ILogger<UpdateJobStatusActivity> logger,
        IJobRepository jobRepository)
    {
        _logger = logger;
        _jobRepository = jobRepository;
    }

    /// <summary>
    /// ジョブのステータスを更新する
    /// </summary>
    /// <param name="update">ステータス更新情報</param>
    [Function(nameof(UpdateJobStatusActivity))]
    public async Task RunAsync([ActivityTrigger] JobStatusUpdate update)
    {
        if (update == null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        if (string.IsNullOrWhiteSpace(update.JobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(update));
        }

        if (string.IsNullOrWhiteSpace(update.Status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(update));
        }

        // ログスコープにJobIdを追加
        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = update.JobId }))
        {
            _logger.LogInformation(
                "Updating job status for JobId: {JobId} to {Status}",
                update.JobId,
                update.Status);

            // Support backward compatibility with CompletedAt
#pragma warning disable CS0618 // Type or member is obsolete
            var finishedAt = update.FinishedAt ?? update.CompletedAt;
#pragma warning restore CS0618 // Type or member is obsolete

            // Update job status in Cosmos DB
            await _jobRepository.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                update.StartedAt,
                finishedAt);

            _logger.LogInformation(
                "Job status updated for JobId: {JobId}",
                update.JobId);
        }
    }
}
