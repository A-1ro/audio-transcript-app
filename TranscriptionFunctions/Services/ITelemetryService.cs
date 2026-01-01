namespace TranscriptionFunctions.Services;

/// <summary>
/// カスタムメトリクスとトレースを Application Insights に送信するサービス
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// 文字起こし処理の成功を記録
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <param name="fileId">ファイルID</param>
    /// <param name="duration">処理時間</param>
    void TrackTranscriptionSuccess(string jobId, string fileId, TimeSpan duration);

    /// <summary>
    /// 文字起こし処理の失敗を記録
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <param name="fileId">ファイルID</param>
    /// <param name="duration">処理時間</param>
    /// <param name="errorMessage">エラーメッセージ</param>
    void TrackTranscriptionFailure(string jobId, string fileId, TimeSpan duration, string errorMessage);

    /// <summary>
    /// ジョブ処理の完了を記録
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <param name="duration">処理時間</param>
    /// <param name="totalFiles">総ファイル数</param>
    /// <param name="successCount">成功数</param>
    /// <param name="failureCount">失敗数</param>
    void TrackJobCompletion(string jobId, TimeSpan duration, int totalFiles, int successCount, int failureCount);
}
