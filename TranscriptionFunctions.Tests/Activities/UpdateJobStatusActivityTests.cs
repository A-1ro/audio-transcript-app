using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// UpdateJobStatusActivityのテスト
/// </summary>
public class UpdateJobStatusActivityTests
{
    private readonly Mock<ILogger<UpdateJobStatusActivity>> _mockLogger;
    private readonly Mock<IJobRepository> _mockJobRepository;
    private readonly UpdateJobStatusActivity _activity;

    public UpdateJobStatusActivityTests()
    {
        _mockLogger = new Mock<ILogger<UpdateJobStatusActivity>>();
        _mockJobRepository = new Mock<IJobRepository>();
        _activity = new UpdateJobStatusActivity(_mockLogger.Object, _mockJobRepository.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidUpdate_CompletesSuccessfully()
    {
        // Arrange
        var update = new JobStatusUpdate
        {
            JobId = "test-job-123",
            Status = JobStatus.Completed,
            FinishedAt = DateTime.UtcNow
        };

        _mockJobRepository
            .Setup(r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                update.StartedAt,
                update.FinishedAt,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _activity.RunAsync(update);

        // Assert
        _mockJobRepository.Verify(
            r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                update.StartedAt,
                update.FinishedAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
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
            FinishedAt = DateTime.UtcNow
        };

        _mockJobRepository
            .Setup(r => r.UpdateJobStatusAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
    [InlineData("", nameof(JobStatus.Completed))]
    [InlineData("job-1", "")]
    public async Task RunAsync_WithInvalidUpdate_ThrowsArgumentException(string jobId, string statusValue)
    {
        // Arrange
        // Use actual constant value if statusValue is a valid constant name, otherwise use empty string
        var status = statusValue == nameof(JobStatus.Completed) ? JobStatus.Completed : statusValue;
        
        var update = new JobStatusUpdate
        {
            JobId = jobId,
            Status = status,
            FinishedAt = DateTime.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(update));
    }

    [Fact]
    public async Task RunAsync_WithStartedAt_PassesStartedAtToRepository()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var update = new JobStatusUpdate
        {
            JobId = "test-job-123",
            Status = JobStatus.Processing,
            StartedAt = startedAt
        };

        _mockJobRepository
            .Setup(r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                startedAt,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _activity.RunAsync(update);

        // Assert
        _mockJobRepository.Verify(
            r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                startedAt,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithFinishedAt_PassesFinishedAtToRepository()
    {
        // Arrange
        var finishedAt = DateTime.UtcNow;
        var update = new JobStatusUpdate
        {
            JobId = "test-job-123",
            Status = JobStatus.Completed,
            FinishedAt = finishedAt
        };

        _mockJobRepository
            .Setup(r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                null,
                finishedAt,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _activity.RunAsync(update);

        // Assert
        _mockJobRepository.Verify(
            r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                null,
                finishedAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
#pragma warning disable CS0618 // Type or member is obsolete
    public async Task RunAsync_WithCompletedAt_UsesItAsFinishedAt()
    {
        // Arrange - test backward compatibility
        var completedAt = DateTime.UtcNow;
        var update = new JobStatusUpdate
        {
            JobId = "test-job-123",
            Status = JobStatus.Completed,
            CompletedAt = completedAt
        };

        _mockJobRepository
            .Setup(r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                null,
                completedAt,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _activity.RunAsync(update);

        // Assert
        _mockJobRepository.Verify(
            r => r.UpdateJobStatusAsync(
                update.JobId,
                update.Status,
                null,
                completedAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
