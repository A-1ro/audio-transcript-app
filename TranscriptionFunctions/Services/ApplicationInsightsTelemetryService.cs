using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Application Insights を使用したテレメトリサービスの実装
/// </summary>
public class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;

    public ApplicationInsightsTelemetryService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
    }

    /// <inheritdoc/>
    public void TrackTranscriptionSuccess(string jobId, string fileId, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("FileId cannot be null or empty", nameof(fileId));
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));

        var properties = new Dictionary<string, string>
        {
            { "JobId", jobId },
            { "FileId", fileId },
            { "Status", "Success" }
        };

        var metrics = new Dictionary<string, double>
        {
            { "DurationMs", duration.TotalMilliseconds }
        };

        _telemetryClient.TrackEvent("TranscriptionCompleted", properties, metrics);
        _telemetryClient.TrackMetric("TranscriptionDuration", duration.TotalMilliseconds, properties);
        _telemetryClient.TrackMetric("TranscriptionSuccessCount", 1, properties);
    }

    /// <inheritdoc/>
    public void TrackTranscriptionFailure(string jobId, string fileId, TimeSpan duration, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("FileId cannot be null or empty", nameof(fileId));
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("ErrorMessage cannot be null or empty", nameof(errorMessage));

        var properties = new Dictionary<string, string>
        {
            { "JobId", jobId },
            { "FileId", fileId },
            { "Status", "Failed" },
            { "ErrorMessage", errorMessage }
        };

        var metrics = new Dictionary<string, double>
        {
            { "DurationMs", duration.TotalMilliseconds }
        };

        _telemetryClient.TrackEvent("TranscriptionFailed", properties, metrics);
        _telemetryClient.TrackMetric("TranscriptionDuration", duration.TotalMilliseconds, properties);
        _telemetryClient.TrackMetric("TranscriptionFailureCount", 1, properties);
    }

    /// <inheritdoc/>
    public void TrackJobCompletion(string jobId, TimeSpan duration, int totalFiles, int successCount, int failureCount)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));
        if (totalFiles < 0)
            throw new ArgumentException("TotalFiles cannot be negative", nameof(totalFiles));
        if (successCount < 0)
            throw new ArgumentException("SuccessCount cannot be negative", nameof(successCount));
        if (failureCount < 0)
            throw new ArgumentException("FailureCount cannot be negative", nameof(failureCount));

        var properties = new Dictionary<string, string>
        {
            { "JobId", jobId },
            { "Status", failureCount == 0 ? "Success" : (successCount > 0 ? "PartiallyFailed" : "Failed") }
        };

        var metrics = new Dictionary<string, double>
        {
            { "DurationMs", duration.TotalMilliseconds },
            { "TotalFiles", totalFiles },
            { "SuccessCount", successCount },
            { "FailureCount", failureCount },
            { "SuccessRate", totalFiles > 0 ? (double)successCount / totalFiles * 100 : 0 }
        };

        _telemetryClient.TrackEvent("JobCompleted", properties, metrics);
        _telemetryClient.TrackMetric("JobDuration", duration.TotalMilliseconds, properties);
        _telemetryClient.TrackMetric("JobSuccessRate", metrics["SuccessRate"], properties);
        
        if (failureCount == 0)
        {
            _telemetryClient.TrackMetric("JobSuccessCount", 1, properties);
        }
        else if (successCount > 0)
        {
            _telemetryClient.TrackMetric("JobPartiallyFailedCount", 1, properties);
        }
        else
        {
            _telemetryClient.TrackMetric("JobFailureCount", 1, properties);
        }
    }
}
