using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// GetAudioFilesActivityのテスト
/// </summary>
public class GetAudioFilesActivityTests
{
    private readonly Mock<IJobRepository> _mockJobRepository;
    private readonly Mock<ILogger<GetAudioFilesActivity>> _mockLogger;
    private readonly GetAudioFilesActivity _activity;

    public GetAudioFilesActivityTests()
    {
        _mockJobRepository = new Mock<IJobRepository>();
        _mockLogger = new Mock<ILogger<GetAudioFilesActivity>>();
        _activity = new GetAudioFilesActivity(_mockJobRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidJobId_ReturnsAudioFiles()
    {
        // Arrange
        var jobId = "test-job-123";
        var audioFiles = new[]
        {
            new AudioFileInfo
            {
                FileId = $"{jobId}-001",
                BlobUrl = "https://storage.blob.core.windows.net/audio/file1.wav",
                FileName = "file1.wav"
            },
            new AudioFileInfo
            {
                FileId = $"{jobId}-002",
                BlobUrl = "https://storage.blob.core.windows.net/audio/file2.wav",
                FileName = "file2.wav"
            }
        };
        var job = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Pending,
            AudioFiles = audioFiles,
            CreatedAt = DateTime.UtcNow
        };

        _mockJobRepository
            .Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        var result = await _activity.RunAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, file =>
        {
            Assert.NotNull(file.FileId);
            Assert.NotNull(file.BlobUrl);
            Assert.Contains(jobId, file.FileId);
        });
    }

    [Fact]
    public async Task RunAsync_WithJobNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "non-existent-job";
        
        _mockJobRepository
            .Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _activity.RunAsync(jobId));
        
        Assert.Contains(jobId, exception.Message);
    }

    [Fact]
    public async Task RunAsync_WithNoAudioFiles_ReturnsEmptyList()
    {
        // Arrange
        var jobId = "test-job-no-files";
        var job = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Pending,
            AudioFiles = null,
            CreatedAt = DateTime.UtcNow
        };

        _mockJobRepository
            .Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        var result = await _activity.RunAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
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
