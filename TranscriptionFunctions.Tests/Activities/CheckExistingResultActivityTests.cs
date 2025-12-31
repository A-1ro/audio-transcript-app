using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// CheckExistingResultActivityのテスト
/// </summary>
public class CheckExistingResultActivityTests
{
    private readonly Mock<ILogger<CheckExistingResultActivity>> _mockLogger;
    private readonly Mock<ITranscriptionRepository> _mockRepository;
    private readonly CheckExistingResultActivity _activity;

    public CheckExistingResultActivityTests()
    {
        _mockLogger = new Mock<ILogger<CheckExistingResultActivity>>();
        _mockRepository = new Mock<ITranscriptionRepository>();
        _activity = new CheckExistingResultActivity(_mockLogger.Object, _mockRepository.Object);
    }

    [Fact]
    public async Task RunAsync_WhenResultExists_ReturnsTranscriptionResult()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-123",
            FileId = "file-001",
            BlobUrl = "https://example.com/audio.wav"
        };

        var existingDocument = new TranscriptionDocument
        {
            Id = "job-123_file-001",
            JobId = "job-123",
            FileId = "file-001",
            TranscriptText = "Existing transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetTranscriptionAsync(
                input.JobId,
                input.FileId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument);

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("file-001", result.FileId);
        Assert.Equal("Existing transcription", result.TranscriptText);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal(TranscriptionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task RunAsync_WhenResultDoesNotExist_ReturnsNull()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-123",
            FileId = "file-001",
            BlobUrl = "https://example.com/audio.wav"
        };

        _mockRepository
            .Setup(r => r.GetTranscriptionAsync(
                input.JobId,
                input.FileId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TranscriptionDocument?)null);

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RunAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _activity.RunAsync(null!));
    }

    [Theory]
    [InlineData("", "file-001", "https://example.com/audio.wav")]
    [InlineData("job-123", "", "https://example.com/audio.wav")]
    public async Task RunAsync_WithInvalidInput_ThrowsArgumentException(
        string jobId,
        string fileId,
        string blobUrl)
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = jobId,
            FileId = fileId,
            BlobUrl = blobUrl
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(input));
    }

    [Fact]
    public async Task RunAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-123",
            FileId = "file-001",
            BlobUrl = "https://example.com/audio.wav"
        };

        _mockRepository
            .Setup(r => r.GetTranscriptionAsync(
                input.JobId,
                input.FileId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _activity.RunAsync(input));
    }

    [Fact]
    public async Task RunAsync_WithFailedStatus_ReturnsFailedResult()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-123",
            FileId = "file-001",
            BlobUrl = "https://example.com/audio.wav"
        };

        var existingDocument = new TranscriptionDocument
        {
            Id = "job-123_file-001",
            JobId = "job-123",
            FileId = "file-001",
            TranscriptText = "",
            Confidence = 0.0,
            Status = TranscriptionStatus.Failed,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetTranscriptionAsync(
                input.JobId,
                input.FileId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument);

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("file-001", result.FileId);
        Assert.Equal("", result.TranscriptText);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(TranscriptionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task RunAsync_LogsCorrectInformation()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-123",
            FileId = "file-001",
            BlobUrl = "https://example.com/audio.wav"
        };

        _mockRepository
            .Setup(r => r.GetTranscriptionAsync(
                input.JobId,
                input.FileId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TranscriptionDocument?)null);

        // Act
        await _activity.RunAsync(input);

        // Assert
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Checking for existing transcription")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
