using Azure;
using Azure.Data.Tables;

namespace TranscriptionFunctions.Models.TableEntities;

/// <summary>
/// Azure Table Storage entity for Transcription Results
/// PartitionKey: JobId
/// RowKey: FileId
/// </summary>
public class TranscriptionEntity : ITableEntity
{
    /// <summary>
    /// Partition Key (JobId for data partitioning)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row Key (FileId for uniqueness within partition)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp managed by Azure Table Storage
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// File ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Transcribed text
    /// </summary>
    public string? TranscriptText { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Transcription status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Raw result from Speech Service (optional)
    /// </summary>
    public string? RawResult { get; set; }

    /// <summary>
    /// Document creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Convert from TranscriptionDocument
    /// </summary>
    public static TranscriptionEntity FromDocument(TranscriptionDocument document)
    {
        return new TranscriptionEntity
        {
            PartitionKey = document.JobId,
            RowKey = document.FileId,
            JobId = document.JobId,
            FileId = document.FileId,
            TranscriptText = document.TranscriptText,
            Confidence = document.Confidence,
            Status = document.Status,
            RawResult = document.RawResult,
            CreatedAt = document.CreatedAt
        };
    }

    /// <summary>
    /// Convert to TranscriptionDocument
    /// </summary>
    public TranscriptionDocument ToDocument()
    {
        return new TranscriptionDocument
        {
            Id = $"{JobId}_{FileId}",
            JobId = JobId,
            FileId = FileId,
            TranscriptText = TranscriptText,
            Confidence = Confidence,
            Status = Status,
            RawResult = RawResult,
            CreatedAt = CreatedAt,
            Timestamp = Timestamp?.ToUnixTimeSeconds()
        };
    }
}
