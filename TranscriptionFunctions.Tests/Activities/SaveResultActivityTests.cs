using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// SaveResultActivityのテスト
/// </summary>
public class SaveResultActivityTests
{
    private readonly Mock<ILogger<SaveResultActivity>> _mockLogger;
    private readonly Mock<ITranscriptionRepository> _mockRepository;
    private readonly SaveResultActivity _activity;

    public SaveResultActivityTests()
    {
        _mockLogger = new Mock<ILogger<SaveResultActivity>>();
        _mockRepository = new Mock<ITranscriptionRepository>();
        _activity = new SaveResultActivity(_mockLogger.Object, _mockRepository.Object);
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

        var savedDocument = new TranscriptionDocument
        {
            Id = "test-job-123_file-001",
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDocument);

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

        // リポジトリが呼ばれたことを確認
        _mockRepository.Verify(
            r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()),
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

        var savedDocument = new TranscriptionDocument
        {
            Id = "test-job-123_file-001",
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            RawResult = "{\"raw\":\"data\"}",
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDocument);

        // Act
        await _activity.RunAsync(input);

        // Assert
        // リポジトリがRawResultを含めて呼ばれたことを確認
        _mockRepository.Verify(
            r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()),
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

        var savedDocument = new TranscriptionDocument
        {
            Id = "test-job-123_file-001",
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "This is a test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDocument);

        // Act
        await _activity.RunAsync(input);

        // Assert
        // リポジトリがnull RawResultで呼ばれたことを確認
        _mockRepository.Verify(
            r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
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

        var savedDocument = new TranscriptionDocument
        {
            Id = $"{jobId}_{fileId}",
            JobId = jobId,
            FileId = fileId,
            TranscriptText = transcriptText,
            Confidence = confidence,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDocument);

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
    [InlineData("job-1", "file-1", "", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-1", "file-1", null, 0.95, TranscriptionStatus.Completed)]
    public async Task RunAsync_WithInvalidInput_ThrowsArgumentException(
        string jobId,
        string fileId,
        string? transcriptText,
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

        var savedDocument = new TranscriptionDocument
        {
            Id = "test-job-123_file-001",
            JobId = "test-job-123",
            FileId = "file-001",
            TranscriptText = "",
            Confidence = 0.0,
            Status = TranscriptionStatus.Failed,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.SaveTranscriptionAsync(
                input.JobId,
                input.FileId,
                input.TranscriptText,
                input.Confidence,
                input.Status,
                input.RawResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDocument);

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
