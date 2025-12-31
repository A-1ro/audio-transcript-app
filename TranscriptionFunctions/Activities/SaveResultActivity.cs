using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 文字起こし結果保存Activity
/// </summary>
public class SaveResultActivity
{
    private readonly ILogger<SaveResultActivity> _logger;
    private readonly ITranscriptionRepository _transcriptionRepository;

    public SaveResultActivity(
        ILogger<SaveResultActivity> logger,
        ITranscriptionRepository transcriptionRepository)
    {
        _logger = logger;
        _transcriptionRepository = transcriptionRepository;
    }

    /// <summary>
    /// 文字起こし結果を永続化する
    /// </summary>
    /// <param name="input">保存する結果情報</param>
    [Function(nameof(SaveResultActivity))]
    public async Task RunAsync([ActivityTrigger] SaveResultInput input)
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

        if (string.IsNullOrWhiteSpace(input.Status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(input));
        }

        // TranscriptText is required when transcription is successful
        if (input.Status == TranscriptionStatus.Completed && string.IsNullOrWhiteSpace(input.TranscriptText))
        {
            throw new ArgumentException("TranscriptText cannot be null or empty when Status is Completed", nameof(input));
        }

        _logger.LogInformation(
            "Saving transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
            input.JobId,
            input.FileId,
            input.Status);

        // Cosmos DB に保存（Upsert操作により冪等性を保証）
        await _transcriptionRepository.SaveTranscriptionAsync(
            input.JobId,
            input.FileId,
            input.TranscriptText,
            input.Confidence,
            input.Status,
            input.RawResult);

        _logger.LogInformation(
            "Transcription result saved to Cosmos DB for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        _logger.LogInformation(
            "Save operation completed for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);
    }
}
