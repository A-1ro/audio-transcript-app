using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Transcription Result Repository Interface
/// </summary>
public interface ITranscriptionRepository
{
    /// <summary>
    /// Check if a transcription result exists for the given JobId and FileId
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="fileId">File ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription document if exists, null otherwise</returns>
    Task<TranscriptionDocument?> GetTranscriptionAsync(
        string jobId,
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save or update a transcription result (idempotent operation)
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="fileId">File ID</param>
    /// <param name="transcriptText">Transcribed text</param>
    /// <param name="confidence">Confidence score</param>
    /// <param name="status">Transcription status</param>
    /// <param name="rawResult">Raw result (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved transcription document</returns>
    Task<TranscriptionDocument> SaveTranscriptionAsync(
        string jobId,
        string fileId,
        string? transcriptText,
        double confidence,
        string status,
        string? rawResult = null,
        CancellationToken cancellationToken = default);
}
