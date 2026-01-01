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
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>既存結果が存在する場合はTranscriptionResult、存在しない場合はnull</returns>
    [Function(nameof(CheckExistingResultActivity))]
    public async Task<TranscriptionResult?> RunAsync(
        [ActivityTrigger] TranscriptionInput input,
        CancellationToken cancellationToken = default)
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
                input.FileId,
                cancellationToken);

            if (existingResult != null)
            {
                _logger.LogInformation(
                    "Found existing transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
                    input.JobId,
                    input.FileId,
                    existingResult.Status);

                // Handle null TranscriptText appropriately based on status
                // Note: TranscriptionDocument (DB entity) allows nullable TranscriptText, but TranscriptionResult
                // has TranscriptText marked as required (non-null). We normalize null from the DB to empty string
                // here to safely bridge that schema/model mismatch.
                // For completed transcriptions, null should not occur but we normalize to empty string
                // For failed transcriptions, null is expected and we preserve it as empty string
                var transcriptText = existingResult.TranscriptText ?? string.Empty;
                if (existingResult.Status == TranscriptionStatus.Completed && existingResult.TranscriptText == null)
                {
                    _logger.LogWarning(
                        "Completed transcription has null TranscriptText for JobId: {JobId}, FileId: {FileId}. Treating as empty string.",
                        input.JobId,
                        input.FileId);
                }

                return new TranscriptionResult
                {
                    FileId = existingResult.FileId,
                    TranscriptText = transcriptText,
                    Confidence = existingResult.Confidence,
                    Status = existingResult.Status,
                    IsExistingResult = true  // Mark as existing to skip re-saving
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
            
            // Re-throw with additional context to aid debugging
            throw new InvalidOperationException(
                $"Failed to check existing transcription result for JobId: {input.JobId}, FileId: {input.FileId}",
                ex);
        }
    }
}
