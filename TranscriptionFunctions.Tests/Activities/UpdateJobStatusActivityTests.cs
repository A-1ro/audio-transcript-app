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
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        // Act & Assert
        await _activity.RunAsync(update);
        // If no exception is thrown, the test passes
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("PartiallyFailed")]
    public async Task RunAsync_WithDifferentStatuses_LogsCorrectly(string status)
    {
        // Arrange
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
}
