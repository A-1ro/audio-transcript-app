using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// SaveResultActivityのテスト
/// </summary>
public class SaveResultActivityTests
{
    private readonly Mock<ILogger<SaveResultActivity>> _mockLogger;
    private readonly SaveResultActivity _activity;

    public SaveResultActivityTests()
    {
        _mockLogger = new Mock<ILogger<SaveResultActivity>>();
        _activity = new SaveResultActivity(_mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidInput_CompletesSuccessfully()
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed
        };

        // Act
        await _activity.RunAsync(input);

        // Assert
        // 完了時のログが記録されることを確認
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Save operation completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithRawResult_SavesRawResultToBlob()
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            RawResult = "{\"raw\":\"data\"}"
        };

        // Act
        await _activity.RunAsync(input);

        // Assert
        // Blob保存のログが記録されることを確認
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Saving raw result to Blob Storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Raw result saved to Blob Storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithoutRawResult_DoesNotSaveToBlob()
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed
        };

        // Act
        await _activity.RunAsync(input);

        // Assert
        // Blob保存のログが記録されないことを確認
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Saving raw result to Blob Storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData("job-1", "file-1", "Text 1", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-2", "file-2", "Text 2", 0.85, TranscriptionStatus.Completed)]
    [InlineData("job-3", "file-3", "", 0.0, TranscriptionStatus.Failed)]
    public async Task RunAsync_WithDifferentInputs_LogsCorrectJobAndFileIds(
        string jobId,
        string fileId,
        string transcriptText,
        double confidence,
        string status)
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = jobId,
            FileId = fileId,
            TranscriptText = transcriptText,
            Confidence = confidence,
            Status = status
        };

        // Act
        await _activity.RunAsync(input);

        // Assert
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains($"JobId: {jobId}") && 
                    v.ToString()!.Contains($"FileId: {fileId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _activity.RunAsync(null!));
    }

    [Theory]
    [InlineData("", "file-1", "text", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-1", "", "text", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-1", "file-1", "text", 0.95, "")]
    public async Task RunAsync_WithInvalidInput_ThrowsArgumentException(
        string jobId,
        string fileId,
        string transcriptText,
        double confidence,
        string status)
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = jobId,
            FileId = fileId,
            TranscriptText = transcriptText,
            Confidence = confidence,
            Status = status
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(input));
    }

    [Fact]
    public async Task RunAsync_IdempotencyCheck_LogsDocumentId()
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "Test",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed
        };
        var expectedDocumentId = "test-job-123_file-001";

        // Act
        await _activity.RunAsync(input);

        // Assert
        // ドキュメントIDでの冪等性チェックがログに記録されることを確認
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedDocumentId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithFailedStatus_SavesCorrectly()
    {
        // Arrange
        var input = new SaveResultInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "",
            Confidence = 0.0,
            Status = TranscriptionStatus.Failed
        };

        // Act
        await _activity.RunAsync(input);

        // Assert
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Status: Failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
