using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// GetJobInfoActivityのテスト
/// </summary>
public class GetJobInfoActivityTests
{
    private readonly Mock<ILogger<GetJobInfoActivity>> _mockLogger;
    private readonly GetJobInfoActivity _activity;

    public GetJobInfoActivityTests()
    {
        _mockLogger = new Mock<ILogger<GetJobInfoActivity>>();
        _activity = new GetJobInfoActivity(_mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidJobId_ReturnsJobInfo()
    {
        // Arrange
        var jobId = "test-job-123";

        // Act
        var result = await _activity.RunAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("Processing", result.Status);
    }

    [Fact]
    public async Task RunAsync_LogsInformation()
    {
        // Arrange
        var jobId = "test-job-456";

        // Act
        await _activity.RunAsync(jobId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(jobId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
