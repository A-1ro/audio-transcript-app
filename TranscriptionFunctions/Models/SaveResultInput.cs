namespace TranscriptionFunctions.Models;

/// <summary>
/// SaveResultActivity入力
/// </summary>
public record SaveResultInput
{
    public required string JobId { get; init; }
    public required string FileId { get; init; }
    public required string TranscriptText { get; init; }
    public required double Confidence { get; init; }
    public required string Status { get; init; }
    public string? RawResult { get; init; }
}
