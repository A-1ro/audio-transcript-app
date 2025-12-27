using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// ジョブステータス更新Activity
/// </summary>
public class UpdateJobStatusActivity
{
    private readonly ILogger<UpdateJobStatusActivity> _logger;

    public UpdateJobStatusActivity(ILogger<UpdateJobStatusActivity> logger)
    {
        _logger = logger;
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

        _logger.LogInformation(
            "Updating job status for JobId: {JobId} to {Status}",
            update.JobId,
            update.Status);

        // TODO: 実際にはCosmosDBのジョブステータスを更新
        // 今回はログのみ
        await Task.Delay(100); // 非同期処理のシミュレーション

        _logger.LogInformation(
            "Job status updated for JobId: {JobId}",
            update.JobId);
    }
}
