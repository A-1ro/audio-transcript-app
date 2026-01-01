using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// ジョブ情報取得Activity
/// </summary>
public class GetJobInfoActivity
{
    private readonly ILogger<GetJobInfoActivity> _logger;

    public GetJobInfoActivity(ILogger<GetJobInfoActivity> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// ジョブ情報を取得する
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <returns>ジョブ情報</returns>
    [Function(nameof(GetJobInfoActivity))]
    public async Task<JobInfo> RunAsync([ActivityTrigger] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        // ログスコープにJobIdを追加
        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = jobId }))
        {
            _logger.LogInformation("Getting job info for JobId: {JobId}", jobId);

            // TODO: 実際にはCosmosDBなどからジョブ情報を取得
            // 今回はモックデータを返す
            await Task.Delay(100); // 非同期処理のシミュレーション

            var jobInfo = new JobInfo
            {
                JobId = jobId,
                Status = JobStatus.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5), // Mock created time
                StartedAt = null,
                FinishedAt = null
            };

            _logger.LogInformation("Job info retrieved for JobId: {JobId}", jobId);

            return jobInfo;
        }
    }
}
