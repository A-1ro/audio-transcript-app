using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 音声文字起こしActivity
/// </summary>
public class TranscribeAudioActivity
{
    private readonly ILogger<TranscribeAudioActivity> _logger;

    public TranscribeAudioActivity(ILogger<TranscribeAudioActivity> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 音声ファイルを文字起こしする
    /// </summary>
    /// <param name="input">文字起こし入力</param>
    /// <returns>文字起こし結果</returns>
    [Function(nameof(TranscribeAudioActivity))]
    public async Task<TranscriptionResult> RunAsync([ActivityTrigger] TranscriptionInput input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.JobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.FileId))
        {
            throw new ArgumentException("FileId cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.BlobUrl))
        {
            throw new ArgumentException("BlobUrl cannot be null or empty", nameof(input));
        }

        _logger.LogInformation(
            "Transcribing audio for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        try
        {
            // TODO: 実際にはAzure AI Speech Serviceを使用して文字起こし
            // 今回はモックデータを返す
            await Task.Delay(500); // 文字起こし処理のシミュレーション

            var result = new TranscriptionResult
            {
                FileId = input.FileId,
                TranscriptText = $"Transcribed text for {input.FileId}",
                Confidence = 0.95,
                Status = TranscriptionStatus.Completed
            };

            _logger.LogInformation(
                "Transcription completed for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Transcription failed for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);

            // 失敗時の結果を返す
            return new TranscriptionResult
            {
                FileId = input.FileId,
                TranscriptText = string.Empty,
                Confidence = 0.0,
                Status = TranscriptionStatus.Failed
            };
        }
    }
}
