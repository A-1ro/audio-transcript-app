using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// UpdateJobStatusActivityのテスト
/// </summary>
public class UpdateJobStatusActivityTests
{
    private readonly Mock<ILogger<UpdateJobStatusActivity>> _mockLogger;
    private readonly UpdateJobStatusActivity _activity;

    public UpdateJobStatusActivityTests()
    {
        _mockLogger = new Mock<ILogger<UpdateJobStatusActivity>>();
        _activity = new UpdateJobStatusActivity(_mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidUpdate_CompletesSuccessfully()
    {
        // Arrange
        var update = new JobStatusUpdate
        {
            JobId = "test-job-123",
            Status = JobStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        // Act & Assert
        await _activity.RunAsync(update);
        // If no exception is thrown, the test passes
    }

    [Theory]
    [InlineData(nameof(JobStatus.Completed))]
    [InlineData(nameof(JobStatus.Failed))]
    [InlineData(nameof(JobStatus.PartiallyFailed))]
    public async Task RunAsync_WithDifferentStatuses_LogsCorrectly(string statusName)
    {
        // Arrange
        // Map the status name to the actual constant value
        var status = statusName switch
        {
            nameof(JobStatus.Completed) => JobStatus.Completed,
            nameof(JobStatus.Failed) => JobStatus.Failed,
            nameof(JobStatus.PartiallyFailed) => JobStatus.PartiallyFailed,
            _ => throw new ArgumentException("Invalid status name", nameof(statusName))
        };

        var update = new JobStatusUpdate
        {
            JobId = "test-job-456",
            Status = status,
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _activity.RunAsync(update);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(status)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_WithNullUpdate_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _activity.RunAsync(null!));
    }

    [Theory]
    [InlineData("", "Completed")]
    [InlineData("job-1", "")]
    public async Task RunAsync_WithInvalidUpdate_ThrowsArgumentException(string jobId, string status)
    {
        // Arrange
        var update = new JobStatusUpdate
        {
            JobId = jobId,
            Status = status,
            CompletedAt = DateTime.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(update));
    }
}
