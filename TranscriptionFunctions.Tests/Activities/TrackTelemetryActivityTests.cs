using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// TrackTelemetryActivityのテスト
/// </summary>
public class TrackTelemetryActivityTests
{
    private readonly Mock<ILogger<TrackTelemetryActivity>> _mockLogger;
    private readonly Mock<ITelemetryService> _mockTelemetryService;
    private readonly TrackTelemetryActivity _activity;

    public TrackTelemetryActivityTests()
    {
        _mockLogger = new Mock<ILogger<TrackTelemetryActivity>>();
        _mockTelemetryService = new Mock<ITelemetryService>();
        _activity = new TrackTelemetryActivity(_mockLogger.Object, _mockTelemetryService.Object);
    }

    [Fact]
    public async Task TrackTranscriptionSuccess_WithValidInput_CallsTelemetryService()
    {
        // Arrange
        var input = new TranscriptionTelemetryInput
        {
            JobId = "test-job-123",
            FileId = "test-file-456",
            Duration = TimeSpan.FromSeconds(10)
        };

        // Act
        await _activity.TrackTranscriptionSuccess(input);

        // Assert
        _mockTelemetryService.Verify(
            s => s.TrackTranscriptionSuccess(
                input.JobId,
                input.FileId,
                input.Duration),
            Times.Once);
    }

    [Fact]
    public async Task TrackTranscriptionSuccess_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _activity.TrackTranscriptionSuccess(null!));
    }

    [Fact]
    public async Task TrackTranscriptionFailure_WithValidInput_CallsTelemetryService()
    {
        // Arrange
        var input = new TranscriptionTelemetryInput
        {
            JobId = "test-job-123",
            FileId = "test-file-456",
            Duration = TimeSpan.FromSeconds(5),
            ErrorMessage = "Test error"
        };

        // Act
        await _activity.TrackTranscriptionFailure(input);

        // Assert
        _mockTelemetryService.Verify(
            s => s.TrackTranscriptionFailure(
                input.JobId,
                input.FileId,
                input.Duration,
                input.ErrorMessage),
            Times.Once);
    }

    [Fact]
    public async Task TrackTranscriptionFailure_WithNullErrorMessage_UsesUnknownError()
    {
        // Arrange
        var input = new TranscriptionTelemetryInput
        {
            JobId = "test-job-123",
            FileId = "test-file-456",
            Duration = TimeSpan.FromSeconds(5),
            ErrorMessage = null
        };

        // Act
        await _activity.TrackTranscriptionFailure(input);

        // Assert
        _mockTelemetryService.Verify(
            s => s.TrackTranscriptionFailure(
                input.JobId,
                input.FileId,
                input.Duration,
                "Unknown error"),
            Times.Once);
    }

    [Fact]
    public async Task TrackTranscriptionFailure_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _activity.TrackTranscriptionFailure(null!));
    }

    [Fact]
    public async Task TrackJobCompletion_WithValidInput_CallsTelemetryService()
    {
        // Arrange
        var input = new JobTelemetryInput
        {
            JobId = "test-job-123",
            Duration = TimeSpan.FromMinutes(2),
            TotalFiles = 10,
            SuccessCount = 8,
            FailureCount = 2
        };

        // Act
        await _activity.TrackJobCompletion(input);

        // Assert
        _mockTelemetryService.Verify(
            s => s.TrackJobCompletion(
                input.JobId,
                input.Duration,
                input.TotalFiles,
                input.SuccessCount,
                input.FailureCount),
            Times.Once);
    }

    [Fact]
    public async Task TrackJobCompletion_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _activity.TrackJobCompletion(null!));
    }
}
