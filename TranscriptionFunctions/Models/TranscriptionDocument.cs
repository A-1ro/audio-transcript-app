using Newtonsoft.Json;

namespace TranscriptionFunctions.Models;

/// <summary>
/// Cosmos DB Transcription Result Document
/// </summary>
public class TranscriptionDocument
{
    /// <summary>
    /// Document ID (format: {JobId}_{FileId} for uniqueness and idempotency)
    /// </summary>
    [JsonProperty("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Job ID (used as partition key)
    /// </summary>
    [JsonProperty("jobId")]
    public required string JobId { get; init; }

    /// <summary>
    /// File ID
    /// </summary>
    [JsonProperty("fileId")]
    public required string FileId { get; init; }

    /// <summary>
    /// Transcribed text
    /// </summary>
    [JsonProperty("transcriptText")]
    public string? TranscriptText { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    [JsonProperty("confidence")]
    public double Confidence { get; init; }

    /// <summary>
    /// Transcription status
    /// </summary>
    [JsonProperty("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Raw result from Speech Service (optional)
    /// </summary>
    [JsonProperty("rawResult")]
    public string? RawResult { get; init; }

    /// <summary>
    /// Document creation timestamp
    /// </summary>
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Cosmos DB _ts (timestamp) - automatically managed by Cosmos DB
    /// </summary>
    [JsonProperty("_ts")]
    public long? Timestamp { get; set; }
}
