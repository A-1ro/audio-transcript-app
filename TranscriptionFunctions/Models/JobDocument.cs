using Newtonsoft.Json;

namespace TranscriptionFunctions.Models;

/// <summary>
/// Cosmos DB Job Document
/// </summary>
public class JobDocument
{
    /// <summary>
    /// Document ID (same as JobId for simplicity)
    /// </summary>
    [JsonProperty("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Job ID (also used as partition key)
    /// </summary>
    [JsonProperty("jobId")]
    public required string JobId { get; init; }

    /// <summary>
    /// Job Status
    /// </summary>
    [JsonProperty("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Job Created Timestamp
    /// </summary>
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Job Started Timestamp (when status changed to Processing)
    /// </summary>
    [JsonProperty("startedAt")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Job Finished Timestamp (when status changed to Completed/Failed/PartiallyFailed)
    /// </summary>
    [JsonProperty("finishedAt")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Cosmos DB _ts (timestamp) - automatically managed by Cosmos DB
    /// </summary>
    [JsonProperty("_ts")]
    public long? Timestamp { get; set; }
}
