namespace TranscriptionFunctions.Models;

/// <summary>
/// ジョブステータス更新入力
/// </summary>
public record JobStatusUpdate
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    public DateTime? CompletedAt { get; init; }
}
