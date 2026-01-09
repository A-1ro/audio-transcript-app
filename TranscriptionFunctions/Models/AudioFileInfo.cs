namespace TranscriptionFunctions.Models;

/// <summary>
/// 音声ファイル情報
/// </summary>
public record AudioFileInfo
{
    public required string FileId { get; init; }
    public required string BlobUrl { get; init; }
    public string? FileName { get; init; }
}
