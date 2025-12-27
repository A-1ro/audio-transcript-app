using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// GetAudioFilesActivityのテスト
/// </summary>
public class GetAudioFilesActivityTests
{
    private readonly Mock<ILogger<GetAudioFilesActivity>> _mockLogger;
    private readonly GetAudioFilesActivity _activity;

    public GetAudioFilesActivityTests()
    {
        _mockLogger = new Mock<ILogger<GetAudioFilesActivity>>();
        _activity = new GetAudioFilesActivity(_mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidJobId_ReturnsAudioFiles()
    {
        // Arrange
        var jobId = "test-job-123";

        // Act
        var result = await _activity.RunAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, file =>
        {
            Assert.NotNull(file.FileId);
            Assert.NotNull(file.BlobUrl);
            Assert.Contains(jobId, file.FileId);
        });
    }

    [Fact]
    public async Task RunAsync_ReturnsExpectedCount()
    {
        // Arrange
        var jobId = "test-job-456";

        // Act
        var result = await _activity.RunAsync(jobId);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_WithInvalidJobId_ThrowsArgumentException(string? invalidJobId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(invalidJobId!));
    }
}
