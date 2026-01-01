using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 音声ファイル一覧取得Activity
/// </summary>
public class GetAudioFilesActivity
{
    private readonly ILogger<GetAudioFilesActivity> _logger;

    public GetAudioFilesActivity(ILogger<GetAudioFilesActivity> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// ジョブに紐づく音声ファイル一覧を取得する
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <returns>音声ファイル一覧</returns>
    [Function(nameof(GetAudioFilesActivity))]
    public async Task<List<AudioFileInfo>> RunAsync([ActivityTrigger] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        // ログスコープにJobIdを追加
        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = jobId }))
        {
            _logger.LogInformation("Getting audio files for JobId: {JobId}", jobId);

            // TODO: 実際にはCosmosDBやBlobStorageから音声ファイル一覧を取得
            // 今回はモックデータを返す
            await Task.Delay(100); // 非同期処理のシミュレーション

            var audioFiles = new List<AudioFileInfo>
            {
                new AudioFileInfo
                {
                    FileId = $"{jobId}-file-001",
                    BlobUrl = $"https://storage.blob.core.windows.net/audio/{jobId}/file-001.wav"
                },
                new AudioFileInfo
                {
                    FileId = $"{jobId}-file-002",
                    BlobUrl = $"https://storage.blob.core.windows.net/audio/{jobId}/file-002.wav"
                }
            };

            _logger.LogInformation("Found {Count} audio files for JobId: {JobId}", audioFiles.Count, jobId);

            return audioFiles;
        }
    }
}
