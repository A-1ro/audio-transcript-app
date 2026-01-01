using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// テレメトリ記録Activity
/// メトリクスとイベントをApplication Insightsに送信する
/// </summary>
public class TrackTelemetryActivity
{
    private const string DefaultErrorMessage = "Unknown error";
    
    private readonly ILogger<TrackTelemetryActivity> _logger;
    private readonly ITelemetryService _telemetryService;

    public TrackTelemetryActivity(
        ILogger<TrackTelemetryActivity> logger,
        ITelemetryService telemetryService)
    {
        _logger = logger;
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// 文字起こし処理の成功を記録
    /// </summary>
    [Function(nameof(TrackTranscriptionSuccess))]
    public Task TrackTranscriptionSuccess([ActivityTrigger] TranscriptionTelemetryInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = input.JobId,
            ["FileId"] = input.FileId
        }))
        {
            _logger.LogInformation(
                "Recording transcription success telemetry for JobId: {JobId}, FileId: {FileId}, Duration: {Duration}ms",
                input.JobId,
                input.FileId,
                input.Duration.TotalMilliseconds);

            _telemetryService.TrackTranscriptionSuccess(
                input.JobId,
                input.FileId,
                input.Duration);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 文字起こし処理の失敗を記録
    /// </summary>
    [Function(nameof(TrackTranscriptionFailure))]
    public Task TrackTranscriptionFailure([ActivityTrigger] TranscriptionTelemetryInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = input.JobId,
            ["FileId"] = input.FileId
        }))
        {
            _logger.LogInformation(
                "Recording transcription failure telemetry for JobId: {JobId}, FileId: {FileId}, Duration: {Duration}ms, Error: {ErrorMessage}",
                input.JobId,
                input.FileId,
                input.Duration.TotalMilliseconds,
                input.ErrorMessage);

            _telemetryService.TrackTranscriptionFailure(
                input.JobId,
                input.FileId,
                input.Duration,
                input.ErrorMessage ?? DefaultErrorMessage);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ジョブ処理の完了を記録
    /// </summary>
    [Function(nameof(TrackJobCompletion))]
    public Task TrackJobCompletion([ActivityTrigger] JobTelemetryInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = input.JobId }))
        {
            _logger.LogInformation(
                "Recording job completion telemetry for JobId: {JobId}, Duration: {Duration}ms, Total: {Total}, Success: {Success}, Failure: {Failure}",
                input.JobId,
                input.Duration.TotalMilliseconds,
                input.TotalFiles,
                input.SuccessCount,
                input.FailureCount);

            _telemetryService.TrackJobCompletion(
                input.JobId,
                input.Duration,
                input.TotalFiles,
                input.SuccessCount,
                input.FailureCount);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 文字起こしテレメトリ入力
/// </summary>
public class TranscriptionTelemetryInput
{
    public required string JobId { get; set; }
    public required string FileId { get; set; }
    public required TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// ジョブテレメトリ入力
/// </summary>
public class JobTelemetryInput
{
    public required string JobId { get; set; }
    public required TimeSpan Duration { get; set; }
    public required int TotalFiles { get; set; }
    public required int SuccessCount { get; set; }
    public required int FailureCount { get; set; }
}
