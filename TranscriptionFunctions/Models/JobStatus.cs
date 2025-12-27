namespace TranscriptionFunctions.Models;

/// <summary>
/// ジョブステータス定数
/// </summary>
public static class JobStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string PartiallyFailed = "PartiallyFailed";
}
