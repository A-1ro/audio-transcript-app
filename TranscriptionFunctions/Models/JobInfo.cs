namespace TranscriptionFunctions.Models;

/// <summary>
/// ジョブ情報
/// </summary>
public record JobInfo
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
