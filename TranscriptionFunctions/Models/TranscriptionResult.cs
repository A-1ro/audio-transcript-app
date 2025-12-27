namespace TranscriptionFunctions.Models;

/// <summary>
/// 文字起こしActivity出力
/// </summary>
public record TranscriptionResult
{
    public required string FileId { get; init; }
    public required string TranscriptText { get; init; }
    public required double Confidence { get; init; }
    public required string Status { get; init; }
}
