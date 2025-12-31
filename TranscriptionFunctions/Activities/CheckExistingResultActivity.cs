using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 既存の文字起こし結果をチェックするActivity
/// 冪等性を実現するため、再実行時に既存結果がある場合はそれを返す
/// </summary>
public class CheckExistingResultActivity
{
    private readonly ILogger<CheckExistingResultActivity> _logger;
    private readonly ITranscriptionRepository _transcriptionRepository;

    public CheckExistingResultActivity(
        ILogger<CheckExistingResultActivity> logger,
        ITranscriptionRepository transcriptionRepository)
    {
        _logger = logger;
        _transcriptionRepository = transcriptionRepository;
    }

    /// <summary>
    /// 既存の文字起こし結果をチェックする
    /// </summary>
    /// <param name="input">チェック対象の入力</param>
    /// <returns>既存結果が存在する場合はTranscriptionResult、存在しない場合はnull</returns>
    [Function(nameof(CheckExistingResultActivity))]
    public async Task<TranscriptionResult?> RunAsync([ActivityTrigger] TranscriptionInput input)
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

        _logger.LogInformation(
            "Checking for existing transcription result for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        try
        {
            var existingResult = await _transcriptionRepository.GetTranscriptionAsync(
                input.JobId,
                input.FileId);

            if (existingResult != null)
            {
                _logger.LogInformation(
                    "Found existing transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
                    input.JobId,
                    input.FileId,
                    existingResult.Status);

                return new TranscriptionResult
                {
                    FileId = existingResult.FileId,
                    TranscriptText = existingResult.TranscriptText ?? string.Empty,
                    Confidence = existingResult.Confidence,
                    Status = existingResult.Status
                };
            }

            _logger.LogInformation(
                "No existing transcription result found for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking for existing transcription result for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);
            throw;
        }
    }
}
