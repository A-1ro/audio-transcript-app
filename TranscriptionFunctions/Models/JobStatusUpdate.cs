namespace TranscriptionFunctions.Models;

/// <summary>
/// ジョブステータス更新入力
/// </summary>
public record JobStatusUpdate
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    
    /// <summary>
    /// Job Started Timestamp (set when status changes to Processing)
    /// </summary>
    public DateTime? StartedAt { get; init; }
    
    /// <summary>
    /// Job Finished Timestamp (set when status changes to Completed/Failed/PartiallyFailed)
    /// </summary>
    public DateTime? FinishedAt { get; init; }
    
    /// <summary>
    /// Deprecated: Use FinishedAt instead
    /// </summary>
    [Obsolete("Use FinishedAt instead")]
    public DateTime? CompletedAt { get; init; }
}
