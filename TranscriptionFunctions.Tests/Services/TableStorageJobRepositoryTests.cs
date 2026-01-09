using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Models.TableEntities;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Services;

/// <summary>
/// TableStorageJobRepositoryのテスト
/// </summary>
public class TableStorageJobRepositoryTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<TableStorageJobRepository>> _mockLogger;
    private const string TestETag = "test-etag-123";

    public TableStorageJobRepositoryTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<TableStorageJobRepository>>();

        // Setup configuration
        _mockConfiguration.Setup(c => c["TableStorage:JobsTableName"]).Returns("Jobs");

        // Setup table service client to return mock table client
        _mockTableServiceClient
            .Setup(c => c.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);
    }

    private Mock<Response<JobEntity>> CreateMockResponse(JobEntity job, string etag = TestETag)
    {
        var mockResponse = new Mock<Response<JobEntity>>();
        job.ETag = new ETag(etag);
        mockResponse.Setup(r => r.Value).Returns(job);
        return mockResponse;
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithValidTransition_UpdatesSuccessfully()
    {
        // Arrange
        var jobId = "test-job-123";
        var currentStatus = JobStatus.Pending;
        var newStatus = JobStatus.Processing;
        var startedAt = DateTime.UtcNow;

        var existingJob = new JobEntity
        {
            PartitionKey = jobId,
            RowKey = jobId,
            JobId = jobId,
            Status = currentStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = null,
            FinishedAt = null,
            ETag = new ETag(TestETag)
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockTableClient
            .Setup(c => c.GetEntityAsync<JobEntity>(
                jobId,
                jobId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockTableClient
            .Setup(c => c.UpdateEntityAsync(
                It.IsAny<JobEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        await repository.UpdateJobStatusAsync(jobId, newStatus, startedAt);

        // Assert
        _mockTableClient.Verify(
            c => c.UpdateEntityAsync(
                It.Is<JobEntity>(j => 
                    j.Status == newStatus && 
                    j.StartedAt == startedAt),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_InvalidTransition_ThrowsException()
    {
        // Arrange
        var jobId = "test-job-123";
        var currentStatus = JobStatus.Completed; // Terminal state
        var newStatus = JobStatus.Processing;

        var existingJob = new JobEntity
        {
            PartitionKey = jobId,
            RowKey = jobId,
            JobId = jobId,
            Status = currentStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ETag = new ETag(TestETag)
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockTableClient
            .Setup(c => c.GetEntityAsync<JobEntity>(
                jobId,
                jobId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await repository.UpdateJobStatusAsync(jobId, newStatus));
    }

    [Fact]
    public async Task UpdateJobStatusAsync_ConcurrentModification_ThrowsException()
    {
        // Arrange
        var jobId = "test-job-123";
        var existingJob = new JobEntity
        {
            PartitionKey = jobId,
            RowKey = jobId,
            JobId = jobId,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ETag = new ETag(TestETag)
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockTableClient
            .Setup(c => c.GetEntityAsync<JobEntity>(
                jobId,
                jobId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Simulate precondition failed (412) for concurrent modification
        _mockTableClient
            .Setup(c => c.UpdateEntityAsync(
                It.IsAny<JobEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(412, "Precondition failed"));

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await repository.UpdateJobStatusAsync(jobId, JobStatus.Processing));
    }

    [Fact]
    public async Task GetJobAsync_WhenExists_ReturnsJob()
    {
        // Arrange
        var jobId = "test-job-123";
        var job = new JobEntity
        {
            PartitionKey = jobId,
            RowKey = jobId,
            JobId = jobId,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var mockResponse = CreateMockResponse(job);

        _mockTableClient
            .Setup(c => c.GetEntityAsync<JobEntity>(
                jobId,
                jobId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetJobAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetJobAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var jobId = "test-job-123";

        _mockTableClient
            .Setup(c => c.GetEntityAsync<JobEntity>(
                jobId,
                jobId,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetJobAsync(jobId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateJobAsync_NewJob_CreatesSuccessfully()
    {
        // Arrange
        var jobId = "test-job-123";
        var audioFiles = new[]
        {
            new AudioFileInfo { FileId = "file1", BlobUrl = "https://example.com/file1", FileName = "test1.mp3" }
        };

        _mockTableClient
            .Setup(c => c.AddEntityAsync(
                It.IsAny<JobEntity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.CreateJobAsync(jobId, audioFiles);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Equal(audioFiles.Length, result.AudioFiles?.Length);

        _mockTableClient.Verify(
            c => c.AddEntityAsync(
                It.Is<JobEntity>(j =>
                    j.JobId == jobId &&
                    j.Status == JobStatus.Pending),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateJobAsync_DuplicateJob_ThrowsException()
    {
        // Arrange
        var jobId = "test-job-123";
        var audioFiles = new[]
        {
            new AudioFileInfo { FileId = "file1", BlobUrl = "https://example.com/file1", FileName = "test1.mp3" }
        };

        // Simulate conflict (409) for duplicate job
        _mockTableClient
            .Setup(c => c.AddEntityAsync(
                It.IsAny<JobEntity>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(409, "Conflict"));

        var repository = new TableStorageJobRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await repository.CreateJobAsync(jobId, audioFiles));
    }
}
