namespace TranscriptionFunctions.Models;

/// <summary>
/// 文字起こしActivity入力
/// </summary>
public record TranscriptionInput
{
    public required string JobId { get; init; }
    public required string FileId { get; init; }
    public required string BlobUrl { get; init; }
}
