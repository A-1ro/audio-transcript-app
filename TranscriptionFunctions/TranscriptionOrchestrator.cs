using System;
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
    /// オーケストレーター内での並列実行数のデフォルト値
    /// この値は環境変数 Transcription:MaxParallelFiles で上書き可能
    /// </summary>
    private const int DefaultMaxParallelFiles = 5;
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

        // RetryOptions設定 - 一時的なエラーに対する再試行
        var retryPolicy = new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0);
        var retryOptions = new TaskOptions(new TaskRetryOptions(retryPolicy));

        try
        {
            // 0. ジョブステータスをProcessingに更新
            logger.LogInformation("Updating job status to Processing for JobId: {JobId}", jobId);
            await context.CallActivityAsync(
                nameof(UpdateJobStatusActivity),
                new JobStatusUpdate
                {
                    JobId = jobId,
                    Status = JobStatus.Processing,
                    StartedAt = context.CurrentUtcDateTime
                },
                retryOptions);

            // 1. ジョブ情報取得
            // NOTE: 現在のオーケストレーターではjobInfoは使用していませんが、
            // 将来的なバリデーションやビジネスロジックのために取得しています
            logger.LogInformation("Retrieving job info for JobId: {JobId}", jobId);
            _ = await context.CallActivityAsync<JobInfo>(
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
                logger.LogWarning("No audio files found for JobId: {JobId}. Marking as Failed.", jobId);
                
                // ファイルが無い場合はデータ整合性の問題として失敗扱い
                await context.CallActivityAsync(
                    nameof(UpdateJobStatusActivity),
                    new JobStatusUpdate
                    {
                        JobId = jobId,
                        Status = JobStatus.Failed,
                        FinishedAt = context.CurrentUtcDateTime
                    },
                    retryOptions);

                return;
            }

            logger.LogInformation("Found {Count} audio files for JobId: {JobId}", audioFiles.Count, jobId);

            // 並列実行数の取得（デフォルト値を使用）
            // NOTE: オーケストレーターの決定性を保つため、環境変数ではなく定数を使用
            // 設定変更が必要な場合は、オーケストレーターの入力パラメータとして渡すか、
            // Activity関数経由で取得する必要があります
            var maxParallelFiles = DefaultMaxParallelFiles;

            logger.LogInformation(
                "Processing {TotalCount} files with max {MaxParallel} parallel executions for JobId: {JobId}",
                audioFiles.Count,
                maxParallelFiles,
                jobId);

            // 3. Activity fan-out - バッチ処理で並列文字起こし実行
            var allResults = new List<TranscriptionResult>();
            
            // ファイルをバッチに分割して処理
            for (int i = 0; i < audioFiles.Count; i += maxParallelFiles)
            {
                var batch = audioFiles.Skip(i).Take(maxParallelFiles).ToList();
                var batchNumber = (i / maxParallelFiles) + 1;
                var totalBatches = (audioFiles.Count + maxParallelFiles - 1) / maxParallelFiles;
                
                logger.LogInformation(
                    "Processing batch {BatchNumber}/{TotalBatches} ({BatchSize} files) for JobId: {JobId}",
                    batchNumber,
                    totalBatches,
                    batch.Count,
                    jobId);

                var transcriptionTasks = batch.Select(audioFile =>
                {
                    var input = new TranscriptionInput
                    {
                        JobId = jobId,
                        FileId = audioFile.FileId,
                        BlobUrl = audioFile.BlobUrl
                    };

                    return context.CallActivityAsync<TranscriptionResult>(
                        nameof(TranscribeAudioActivity),
                        input,
                        retryOptions);
                }).ToList();

                // バッチ内の全タスクが完了するまで待機
                var batchResults = await Task.WhenAll(transcriptionTasks);
                allResults.AddRange(batchResults);
                
                logger.LogInformation(
                    "Completed batch {BatchNumber}/{TotalBatches} for JobId: {JobId}",
                    batchNumber,
                    totalBatches,
                    jobId);
            }

            // 4. 結果集約 - fan-in
            logger.LogInformation("All transcription batches completed for JobId: {JobId}", jobId);
            var results = allResults.ToArray();

            // 5. 結果保存 - Cosmos DBに永続化
            logger.LogInformation("Saving transcription results for JobId: {JobId}", jobId);
            var saveResultTasks = results.Select(result =>
            {
                var saveInput = new SaveResultInput
                {
                    JobId = jobId,
                    FileId = result.FileId,
                    TranscriptText = result.TranscriptText,
                    Confidence = result.Confidence,
                    Status = result.Status
                };

                return context.CallActivityAsync(
                    nameof(SaveResultActivity),
                    saveInput,
                    retryOptions);
            }).ToList();

            await Task.WhenAll(saveResultTasks);
            logger.LogInformation("All transcription results saved for JobId: {JobId}", jobId);

            // 6. 結果分析と状態決定
            var successCount = results.Count(r => r.Status == TranscriptionStatus.Completed);
            var failureCount = results.Count(r => r.Status == TranscriptionStatus.Failed);
            var unexpectedCount = results.Count(r => 
                r.Status != TranscriptionStatus.Completed && 
                r.Status != TranscriptionStatus.Failed);
            var totalCount = results.Length;

            if (unexpectedCount > 0)
            {
                logger.LogWarning(
                    "Found {UnexpectedCount} results with unexpected status for JobId: {JobId}",
                    unexpectedCount,
                    jobId);
            }

            logger.LogInformation(
                "Transcription results for JobId: {JobId} - Success: {SuccessCount}, Failed: {FailureCount}, Unexpected: {UnexpectedCount}, Total: {TotalCount}",
                jobId,
                successCount,
                failureCount,
                unexpectedCount,
                totalCount);

            // 7. ジョブステータス更新
            string finalStatus;
            if (failureCount == 0 && unexpectedCount == 0)
            {
                // 全て成功
                finalStatus = JobStatus.Completed;
            }
            else if (successCount > 0)
            {
                // 一部成功、一部失敗または予期しないステータス
                finalStatus = JobStatus.PartiallyFailed;
            }
            else
            {
                // 全て失敗または予期しないステータス
                finalStatus = JobStatus.Failed;
            }

            await context.CallActivityAsync(
                nameof(UpdateJobStatusActivity),
                new JobStatusUpdate
                {
                    JobId = jobId,
                    Status = finalStatus,
                    FinishedAt = context.CurrentUtcDateTime
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
            // NOTE: ステータス更新に失敗した場合でも、元の例外を再スローしてオーケストレーション全体を失敗させます。
            // これにより、Durable Functionsの再試行メカニズムが全体のオーケストレーションを再実行します。
            try
            {
                await context.CallActivityAsync(
                    nameof(UpdateJobStatusActivity),
                    new JobStatusUpdate
                    {
                        JobId = jobId,
                        Status = JobStatus.Failed,
                        FinishedAt = context.CurrentUtcDateTime
                    },
                    retryOptions);
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update job status to Failed for JobId: {JobId}",
                    jobId);
                // ステータス更新の失敗は記録するが、元の例外を優先して再スロー
            }

            throw;
        }
    }
}
