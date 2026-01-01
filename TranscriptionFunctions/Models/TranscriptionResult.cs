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
    
    /// <summary>
    /// 既存の結果から取得したかどうか（true = 既存、false = 新規文字起こし）
    /// 既存結果の場合は再保存をスキップして不要なDB書き込みを削減
    /// </summary>
    public bool IsExistingResult { get; init; }
}
