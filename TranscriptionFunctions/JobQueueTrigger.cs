using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Constants;

namespace TranscriptionFunctions;

/// <summary>
/// ジョブ起動トリガー - QueueメッセージからDurable Orchestratorを起動
/// </summary>
public class JobQueueTrigger
{
    private readonly ILogger<JobQueueTrigger> _logger;

    public JobQueueTrigger(ILogger<JobQueueTrigger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Queueメッセージを受け取り、TranscriptionOrchestratorを起動する
    /// </summary>
    /// <param name="queueMessage">JobIdを含むメッセージ</param>
    /// <param name="client">Durable Task Client</param>
    [Function(nameof(JobQueueTrigger))]
    public async Task RunAsync(
        [QueueTrigger(QueueNames.TranscriptionJobs, Connection = "AzureWebJobsStorage")] string queueMessage,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Queue trigger received message: {Message}", queueMessage);

        // JobIdを取得 (メッセージがそのままJobIdと仮定)
        var jobId = queueMessage;

        if (string.IsNullOrWhiteSpace(jobId))
        {
            _logger.LogError("JobId is empty or null");
            throw new ArgumentException("JobId cannot be empty", nameof(queueMessage));
        }

        // ログスコープにJobIdを追加 - エラーログにもJobIdが含まれるようにtry-catchを包含
        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = jobId }))
        {
            try
            {
                // TranscriptionOrchestratorを起動
                var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                    nameof(TranscriptionOrchestrator),
                    jobId);

                _logger.LogInformation(
                    "Started orchestration with ID = '{InstanceId}' for JobId = '{JobId}'",
                    instanceId,
                    jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start orchestration for message: {Message}",
                    queueMessage);
                throw;
            }
        }
    }
}
