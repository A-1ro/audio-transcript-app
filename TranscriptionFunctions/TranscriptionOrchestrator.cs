using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

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
            // TODO: ジョブ情報取得
            // TODO: 音声ファイル一覧取得
            // TODO: Activity fan-out (並列文字起こし)
            // TODO: 結果集約
            // TODO: 状態更新

            logger.LogInformation("Transcription orchestration completed for JobId: {JobId}", jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Transcription orchestration failed for JobId: {JobId}",
                jobId);
            throw;
        }
    }
}
