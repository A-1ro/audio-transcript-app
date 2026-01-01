using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Services;

/// <summary>
/// ApplicationInsightsTelemetryServiceのテスト
/// </summary>
public class ApplicationInsightsTelemetryServiceTests
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ApplicationInsightsTelemetryService _service;
    private readonly List<ITelemetry> _telemetryItems;

    public ApplicationInsightsTelemetryServiceTests()
    {
        // テスト用のTelemetryClientを作成
        var telemetryConfig = new TelemetryConfiguration
        {
            TelemetryChannel = new StubTelemetryChannel()
        };
        _telemetryClient = new TelemetryClient(telemetryConfig);
        _telemetryItems = new List<ITelemetry>();
        ((StubTelemetryChannel)telemetryConfig.TelemetryChannel).OnSend = item => _telemetryItems.Add(item);
        
        _service = new ApplicationInsightsTelemetryService(_telemetryClient);
    }

    [Fact]
    public void Constructor_WithNullTelemetryClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ApplicationInsightsTelemetryService(null!));
    }

    [Fact]
    public void TrackTranscriptionSuccess_WithValidInput_TracksEventAndMetrics()
    {
        // Arrange
        var jobId = "test-job-123";
        var fileId = "test-file-456";
        var duration = TimeSpan.FromSeconds(10);

        // Act
        _service.TrackTranscriptionSuccess(jobId, fileId, duration);
        _telemetryClient.Flush();

        // Assert
        Assert.NotEmpty(_telemetryItems);
        
        var events = _telemetryItems.OfType<EventTelemetry>().ToList();
        var metrics = _telemetryItems.OfType<MetricTelemetry>().ToList();
        
        // EventTelemetryが記録されることを確認
        var transcriptionEvent = events.FirstOrDefault(e => e.Name == "TranscriptionCompleted");
        Assert.NotNull(transcriptionEvent);
        Assert.Equal("Success", transcriptionEvent.Properties["Status"]);
        Assert.Equal(jobId, transcriptionEvent.Properties["JobId"]);
        Assert.Equal(fileId, transcriptionEvent.Properties["FileId"]);
        
        // MetricTelemetryが記録されることを確認
        var durationMetric = metrics.FirstOrDefault(m => m.Name == "TranscriptionDuration");
        Assert.NotNull(durationMetric);
        
        var successMetric = metrics.FirstOrDefault(m => m.Name == "TranscriptionSuccessCount");
        Assert.NotNull(successMetric);
    }

    [Fact]
    public void TrackTranscriptionFailure_WithValidInput_TracksEventAndMetrics()
    {
        // Arrange
        var jobId = "test-job-123";
        var fileId = "test-file-456";
        var duration = TimeSpan.FromSeconds(5);
        var errorMessage = "Test error message";

        // Act
        _service.TrackTranscriptionFailure(jobId, fileId, duration, errorMessage);
        _telemetryClient.Flush();

        // Assert
        Assert.NotEmpty(_telemetryItems);
        
        var events = _telemetryItems.OfType<EventTelemetry>().ToList();
        var metrics = _telemetryItems.OfType<MetricTelemetry>().ToList();
        
        // EventTelemetryが記録されることを確認
        var transcriptionEvent = events.FirstOrDefault(e => e.Name == "TranscriptionFailed");
        Assert.NotNull(transcriptionEvent);
        Assert.Equal("Failed", transcriptionEvent.Properties["Status"]);
        Assert.Equal(jobId, transcriptionEvent.Properties["JobId"]);
        Assert.Equal(fileId, transcriptionEvent.Properties["FileId"]);
        Assert.Equal(errorMessage, transcriptionEvent.Properties["ErrorMessage"]);
        
        // MetricTelemetryが記録されることを確認
        var failureMetric = metrics.FirstOrDefault(m => m.Name == "TranscriptionFailureCount");
        Assert.NotNull(failureMetric);
    }

    [Fact]
    public void TrackJobCompletion_WithAllSuccess_TracksSuccessMetrics()
    {
        // Arrange
        var jobId = "test-job-123";
        var duration = TimeSpan.FromMinutes(2);
        var totalFiles = 10;
        var successCount = 10;
        var failureCount = 0;

        // Act
        _service.TrackJobCompletion(jobId, duration, totalFiles, successCount, failureCount);
        _telemetryClient.Flush();

        // Assert
        Assert.NotEmpty(_telemetryItems);
        
        var events = _telemetryItems.OfType<EventTelemetry>().ToList();
        var metrics = _telemetryItems.OfType<MetricTelemetry>().ToList();
        
        // EventTelemetryが記録されることを確認
        var jobEvent = events.FirstOrDefault(e => e.Name == "JobCompleted");
        Assert.NotNull(jobEvent);
        Assert.Equal("Success", jobEvent.Properties["Status"]);
        Assert.Equal(jobId, jobEvent.Properties["JobId"]);
        
        // SuccessRateが100%であることを確認
        var successRateMetric = metrics.FirstOrDefault(m => m.Name == "JobSuccessRate");
        Assert.NotNull(successRateMetric);
        Assert.Equal(100.0, successRateMetric.Sum);
        
        // JobSuccessCountが記録されることを確認
        var successMetric = metrics.FirstOrDefault(m => m.Name == "JobSuccessCount");
        Assert.NotNull(successMetric);
    }

    [Fact]
    public void TrackJobCompletion_WithPartialFailure_TracksPartiallyFailedMetrics()
    {
        // Arrange
        var jobId = "test-job-123";
        var duration = TimeSpan.FromMinutes(2);
        var totalFiles = 10;
        var successCount = 7;
        var failureCount = 3;

        // Act
        _service.TrackJobCompletion(jobId, duration, totalFiles, successCount, failureCount);
        _telemetryClient.Flush();

        // Assert
        var events = _telemetryItems.OfType<EventTelemetry>().ToList();
        var metrics = _telemetryItems.OfType<MetricTelemetry>().ToList();
        
        var jobEvent = events.FirstOrDefault(e => e.Name == "JobCompleted");
        Assert.NotNull(jobEvent);
        Assert.Equal("PartiallyFailed", jobEvent.Properties["Status"]);
        
        // SuccessRateが70%であることを確認
        var successRateMetric = metrics.FirstOrDefault(m => m.Name == "JobSuccessRate");
        Assert.NotNull(successRateMetric);
        Assert.Equal(70.0, successRateMetric.Sum);
        
        // JobPartiallyFailedCountが記録されることを確認
        var partiallyFailedMetric = metrics.FirstOrDefault(m => m.Name == "JobPartiallyFailedCount");
        Assert.NotNull(partiallyFailedMetric);
    }

    [Fact]
    public void TrackJobCompletion_WithAllFailure_TracksFailedMetrics()
    {
        // Arrange
        var jobId = "test-job-123";
        var duration = TimeSpan.FromMinutes(2);
        var totalFiles = 10;
        var successCount = 0;
        var failureCount = 10;

        // Act
        _service.TrackJobCompletion(jobId, duration, totalFiles, successCount, failureCount);
        _telemetryClient.Flush();

        // Assert
        var events = _telemetryItems.OfType<EventTelemetry>().ToList();
        var metrics = _telemetryItems.OfType<MetricTelemetry>().ToList();
        
        var jobEvent = events.FirstOrDefault(e => e.Name == "JobCompleted");
        Assert.NotNull(jobEvent);
        Assert.Equal("Failed", jobEvent.Properties["Status"]);
        
        // SuccessRateが0%であることを確認
        var successRateMetric = metrics.FirstOrDefault(m => m.Name == "JobSuccessRate");
        Assert.NotNull(successRateMetric);
        Assert.Equal(0.0, successRateMetric.Sum);
        
        // JobFailureCountが記録されることを確認
        var failureMetric = metrics.FirstOrDefault(m => m.Name == "JobFailureCount");
        Assert.NotNull(failureMetric);
    }
}

/// <summary>
/// テスト用のTelemetryChannelスタブ
/// </summary>
public class StubTelemetryChannel : ITelemetryChannel
{
    public Action<ITelemetry>? OnSend { get; set; }
    
    public bool? DeveloperMode { get; set; }
    public string? EndpointAddress { get; set; }

    public void Send(ITelemetry item)
    {
        OnSend?.Invoke(item);
    }

    public void Flush()
    {
        // No-op for testing
    }

    public void Dispose()
    {
        // No-op for testing
    }
}
