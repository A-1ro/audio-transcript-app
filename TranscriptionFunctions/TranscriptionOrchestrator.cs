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
    /// オーケストレーター内での並列実行数
    /// NOTE: オーケストレーターの決定性を保つため、この値は定数として定義されています。
    /// 動的な設定変更が必要な場合は、オーケストレーターの入力パラメータとして渡すか、
    /// Activity関数経由で取得する必要があります。
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

        // 開始時刻を記録
        var orchestrationStartTime = context.CurrentUtcDateTime;

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

                // テレメトリを記録 - ファイルが見つからなかった場合
                await context.CallActivityAsync(
                    nameof(TrackTelemetryActivity.TrackJobCompletion),
                    new JobTelemetryInput
                    {
                        JobId = jobId,
                        Duration = context.CurrentUtcDateTime - orchestrationStartTime,
                        TotalFiles = 0,
                        SuccessCount = 0,
                        FailureCount = 1  // Job-level failure
                    });

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

                // 各ファイルに対して冪等性チェックと文字起こしタスクを作成
                // 決定性を保つため、まず全ての既存結果チェックタスクを作成
                var checkTasks = batch.Select(audioFile =>
                {
                    var input = new TranscriptionInput
                    {
                        JobId = jobId,
                        FileId = audioFile.FileId,
                        BlobUrl = audioFile.BlobUrl
                    };

                    return context.CallActivityAsync<TranscriptionResult?>(
                        nameof(CheckExistingResultActivity),
                        input,
                        retryOptions);
                }).ToList();

                // 全てのチェックタスクが完了するまで待機
                var checkResults = await Task.WhenAll(checkTasks);

                // チェック結果に基づいて文字起こしタスクを作成
                var transcriptionTasks = new List<Task<TranscriptionResult>>();
                for (int fileIndex = 0; fileIndex < batch.Count; fileIndex++)
                {
                    var audioFile = batch[fileIndex];
                    var existingResult = checkResults[fileIndex];

                    if (existingResult != null)
                    {
                        logger.LogInformation(
                            "Using existing transcription result for JobId: {JobId}, FileId: {FileId}",
                            jobId,
                            audioFile.FileId);
                        // 既存結果をタスクとして返す
                        transcriptionTasks.Add(Task.FromResult(existingResult));
                    }
                    else
                    {
                        // 既存結果がない場合のみ新規文字起こしを実行
                        logger.LogInformation(
                            "No existing result found, transcribing audio for JobId: {JobId}, FileId: {FileId}",
                            jobId,
                            audioFile.FileId);

                        var input = new TranscriptionInput
                        {
                            JobId = jobId,
                            FileId = audioFile.FileId,
                            BlobUrl = audioFile.BlobUrl
                        };

                        transcriptionTasks.Add(context.CallActivityAsync<TranscriptionResult>(
                            nameof(TranscribeAudioActivity),
                            input,
                            retryOptions));
                    }
                }

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

            // 5. 結果保存 - Cosmos DBに永続化（新規結果のみ）
            // 既存結果は既にDBに存在するため、再保存をスキップして不要なDB書き込みを削減
            var newResults = results.Where(r => !r.IsExistingResult).ToArray();
            var existingResultsCount = results.Length - newResults.Length;
            
            logger.LogInformation(
                "Saving {NewCount} new transcription results for JobId: {JobId} (skipping {ExistingCount} existing results)",
                newResults.Length,
                jobId,
                existingResultsCount);
            
            var saveResultTasks = newResults.Select(result =>
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
            logger.LogInformation("All new transcription results saved for JobId: {JobId}", jobId);

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

            // ジョブ完了のテレメトリを記録
            var jobDuration = context.CurrentUtcDateTime - orchestrationStartTime;
            await context.CallActivityAsync(
                nameof(TrackTelemetryActivity.TrackJobCompletion),
                new JobTelemetryInput
                {
                    JobId = jobId,
                    Duration = jobDuration,
                    TotalFiles = totalCount,
                    SuccessCount = successCount,
                    FailureCount = failureCount
                });

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
                
                // 失敗したジョブのテレメトリを記録
                await context.CallActivityAsync(
                    nameof(TrackTelemetryActivity.TrackJobCompletion),
                    new JobTelemetryInput
                    {
                        JobId = jobId,
                        Duration = context.CurrentUtcDateTime - orchestrationStartTime,
                        TotalFiles = 0,
                        SuccessCount = 0,
                        FailureCount = 1  // Job-level failure (orchestration exception)
                    });
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
