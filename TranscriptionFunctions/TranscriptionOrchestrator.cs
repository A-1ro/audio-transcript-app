using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions;

/// <summary>
/// 文字起こしジョブのオーケストレーター
/// ジョブ単位の処理全体を制御する
/// </summary>
public class TranscriptionOrchestrator
{
    /// <summary>
    /// オーケストレーション本体
    /// </summary>
    /// <param name="context">Orchestration Context</param>
    /// <param name="jobId">処理対象のJobId</param>
    [Function(nameof(TranscriptionOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        string jobId)
    {
        var logger = context.CreateReplaySafeLogger<TranscriptionOrchestrator>();

        logger.LogInformation("Starting transcription orchestration for JobId: {JobId}", jobId);

        try
        {
            // RetryOptions設定 - 一時的なエラーに対する再試行
            var retryPolicy = new RetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5),
                backoffCoefficient: 2.0);
            var retryOptions = new TaskOptions(new TaskRetryOptions(retryPolicy));

            // 1. ジョブ情報取得
            logger.LogInformation("Retrieving job info for JobId: {JobId}", jobId);
            var jobInfo = await context.CallActivityAsync<JobInfo>(
                nameof(GetJobInfoActivity),
                jobId,
                retryOptions);

            // 2. 音声ファイル一覧取得
            logger.LogInformation("Retrieving audio files for JobId: {JobId}", jobId);
            var audioFiles = await context.CallActivityAsync<List<AudioFileInfo>>(
                nameof(GetAudioFilesActivity),
                jobId,
                retryOptions);

            if (audioFiles == null || audioFiles.Count == 0)
            {
                logger.LogWarning("No audio files found for JobId: {JobId}", jobId);
                
                // ファイルが無い場合は完了扱い
                await context.CallActivityAsync(
                    nameof(UpdateJobStatusActivity),
                    new JobStatusUpdate
                    {
                        JobId = jobId,
                        Status = "Completed",
                        CompletedAt = context.CurrentUtcDateTime
                    },
                    retryOptions);

                return;
            }

            logger.LogInformation("Found {Count} audio files for JobId: {JobId}", audioFiles.Count, jobId);

            // 3. Activity fan-out - 並列文字起こし実行
            var transcriptionTasks = new List<Task<TranscriptionResult>>();

            foreach (var audioFile in audioFiles)
            {
                var input = new TranscriptionInput
                {
                    JobId = jobId,
                    FileId = audioFile.FileId,
                    BlobUrl = audioFile.BlobUrl
                };

                var task = context.CallActivityAsync<TranscriptionResult>(
                    nameof(TranscribeAudioActivity),
                    input,
                    retryOptions);

                transcriptionTasks.Add(task);
            }

            // 4. 結果集約 - fan-in
            logger.LogInformation("Waiting for transcription results for JobId: {JobId}", jobId);
            var results = await Task.WhenAll(transcriptionTasks);

            // 5. 結果分析と状態決定
            var successCount = results.Count(r => r.Status == "Completed");
            var failureCount = results.Count(r => r.Status == "Failed");
            var totalCount = results.Length;

            logger.LogInformation(
                "Transcription results for JobId: {JobId} - Success: {SuccessCount}, Failed: {FailureCount}, Total: {TotalCount}",
                jobId,
                successCount,
                failureCount,
                totalCount);

            // 6. ジョブステータス更新
            string finalStatus;
            if (failureCount == 0)
            {
                // 全て成功
                finalStatus = "Completed";
            }
            else if (successCount > 0)
            {
                // 一部成功、一部失敗
                finalStatus = "PartiallyFailed";
            }
            else
            {
                // 全て失敗
                finalStatus = "Failed";
            }

            await context.CallActivityAsync(
                nameof(UpdateJobStatusActivity),
                new JobStatusUpdate
                {
                    JobId = jobId,
                    Status = finalStatus,
                    CompletedAt = context.CurrentUtcDateTime
                },
                retryOptions);

            logger.LogInformation(
                "Transcription orchestration completed for JobId: {JobId} with status: {Status}",
                jobId,
                finalStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Transcription orchestration failed for JobId: {JobId}",
                jobId);

            // エラー時のステータス更新
            try
            {
                await context.CallActivityAsync(
                    nameof(UpdateJobStatusActivity),
                    new JobStatusUpdate
                    {
                        JobId = jobId,
                        Status = "Failed",
                        CompletedAt = context.CurrentUtcDateTime
                    });
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update job status to Failed for JobId: {JobId}",
                    jobId);
            }

            throw;
        }
    }
}
