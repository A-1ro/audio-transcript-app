using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Services;

/// <summary>
/// CosmosDbJobRepositoryのテスト
/// </summary>
public class CosmosDbJobRepositoryTests
{
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<Container> _mockContainer;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CosmosDbJobRepository>> _mockLogger;
    private const string TestETag = "test-etag-123";

    public CosmosDbJobRepositoryTests()
    {
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockContainer = new Mock<Container>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CosmosDbJobRepository>>();

        // Setup configuration
        _mockConfiguration.Setup(c => c["CosmosDb:DatabaseName"]).Returns("TranscriptionDb");
        _mockConfiguration.Setup(c => c["CosmosDb:JobsContainerName"]).Returns("Jobs");

        // Setup cosmos client to return mock container
        _mockCosmosClient
            .Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_mockContainer.Object);
    }

    private Mock<ItemResponse<JobDocument>> CreateMockResponse(JobDocument job, string etag = TestETag)
    {
        var mockResponse = new Mock<ItemResponse<JobDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(job);
        mockResponse.Setup(r => r.ETag).Returns(etag);
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

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = currentStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = null,
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        await repository.UpdateJobStatusAsync(jobId, newStatus, startedAt);

        // Assert
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.Is<JobDocument>(j => 
                    j.Status == newStatus && 
                    j.StartedAt == startedAt),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithInvalidTransition_ThrowsException()
    {
        // Arrange
        var jobId = "test-job-123";
        var currentStatus = JobStatus.Completed;
        var newStatus = JobStatus.Processing; // Invalid: can't go from Completed to Processing

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = currentStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateJobStatusAsync(jobId, newStatus));
    }

    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.Processing)]
    [InlineData(JobStatus.Pending, JobStatus.Failed)]
    [InlineData(JobStatus.Processing, JobStatus.Completed)]
    [InlineData(JobStatus.Processing, JobStatus.Failed)]
    [InlineData(JobStatus.Processing, JobStatus.PartiallyFailed)]
    public async Task UpdateJobStatusAsync_WithValidTransitions_Succeeds(string fromStatus, string toStatus)
    {
        // Arrange
        var jobId = "test-job-123";

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = fromStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = null,
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act - should not throw
        await repository.UpdateJobStatusAsync(jobId, toStatus);

        // Assert
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.Is<JobDocument>(j => j.Status == toStatus),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(JobStatus.Completed, JobStatus.Pending)]
    [InlineData(JobStatus.Completed, JobStatus.Processing)]
    [InlineData(JobStatus.Failed, JobStatus.Pending)]
    [InlineData(JobStatus.Failed, JobStatus.Processing)]
    [InlineData(JobStatus.PartiallyFailed, JobStatus.Pending)]
    public async Task UpdateJobStatusAsync_WithInvalidTransitions_ThrowsException(string fromStatus, string toStatus)
    {
        // Arrange
        var jobId = "test-job-123";

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = fromStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateJobStatusAsync(jobId, toStatus));
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithSameStatus_AllowsIdempotency()
    {
        // Arrange
        var jobId = "test-job-123";
        var status = JobStatus.Processing;

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act - should not throw (idempotency)
        await repository.UpdateJobStatusAsync(jobId, status);

        // Assert
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithFinishedStatus_SetsFinishedAt()
    {
        // Arrange
        var jobId = "test-job-123";
        var finishedAt = DateTime.UtcNow;

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Processing,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        await repository.UpdateJobStatusAsync(jobId, JobStatus.Completed, finishedAt: finishedAt);

        // Assert
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.Is<JobDocument>(j => 
                    j.Status == JobStatus.Completed && 
                    j.FinishedAt == finishedAt),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithStartedAtAlreadySet_PreservesOriginalValue()
    {
        // Arrange
        var jobId = "test-job-123";
        var originalStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var newStartedAt = DateTime.UtcNow;

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Processing,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            StartedAt = originalStartedAt, // Already set
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act - retry with same status but new timestamp
        await repository.UpdateJobStatusAsync(jobId, JobStatus.Processing, newStartedAt);

        // Assert - original timestamp should be preserved
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.Is<JobDocument>(j => 
                    j.Status == JobStatus.Processing && 
                    j.StartedAt == originalStartedAt), // Original value preserved
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithFinishedAtAlreadySet_PreservesOriginalValue()
    {
        // Arrange
        var jobId = "test-job-123";
        var originalFinishedAt = DateTime.UtcNow.AddMinutes(-5);
        var newFinishedAt = DateTime.UtcNow;

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            FinishedAt = originalFinishedAt // Already set
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act - retry with same status but new timestamp
        await repository.UpdateJobStatusAsync(jobId, JobStatus.Completed, finishedAt: newFinishedAt);

        // Assert - original timestamp should be preserved
        _mockContainer.Verify(
            c => c.ReplaceItemAsync(
                It.Is<JobDocument>(j => 
                    j.Status == JobStatus.Completed && 
                    j.FinishedAt == originalFinishedAt), // Original value preserved
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithConcurrentUpdate_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job-123";

        var existingJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Processing,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(existingJob);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<JobDocument>(),
                jobId,
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(opts => opts.IfMatchEtag == TestETag),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Precondition failed", System.Net.HttpStatusCode.PreconditionFailed, 0, "", 0));

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateJobStatusAsync(jobId, JobStatus.Completed, finishedAt: DateTime.UtcNow));

        Assert.Contains("modified by another process", exception.Message);
    }

    [Fact]
    public async Task GetJobAsync_WithExistingJob_ReturnsJob()
    {
        // Arrange
        var jobId = "test-job-123";
        var job = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Processing,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            FinishedAt = null
        };

        var mockResponse = CreateMockResponse(job);

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetJobAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobStatus.Processing, result.Status);
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistingJob_ReturnsNull()
    {
        // Arrange
        var jobId = "non-existing-job";

        _mockContainer
            .Setup(c => c.ReadItemAsync<JobDocument>(
                jobId,
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetJobAsync(jobId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllJobsAsync_ReturnsJobsOrderedByCreatedAtDescending()
    {
        // Arrange - jobs ordered by createdAt descending
        var jobs = new List<JobDocument>
        {
            new JobDocument
            {
                Id = "job-3",
                JobId = "job-3",
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)  // Most recent
            },
            new JobDocument
            {
                Id = "job-2",
                JobId = "job-2",
                Status = JobStatus.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new JobDocument
            {
                Id = "job-1",
                JobId = "job-1",
                Status = JobStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)  // Oldest
            }
        };

        var mockFeedResponse = new Mock<FeedResponse<JobDocument>>();
        mockFeedResponse.Setup(r => r.GetEnumerator()).Returns(jobs.GetEnumerator());
        mockFeedResponse.Setup(r => r.RequestCharge).Returns(2.5);

        var mockIterator = new Mock<FeedIterator<JobDocument>>();
        mockIterator.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);
        mockIterator
            .Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        _mockContainer
            .Setup(c => c.GetItemQueryIterator<JobDocument>(
                It.IsAny<QueryDefinition>(),
                null,
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetAllJobsAsync();

        // Assert
        Assert.NotNull(result);
        var jobList = result.ToList();
        Assert.Equal(3, jobList.Count);
        Assert.Equal("job-3", jobList[0].JobId);
        Assert.Equal("job-2", jobList[1].JobId);
        Assert.Equal("job-1", jobList[2].JobId);
    }

    [Fact]
    public async Task GetAllJobsAsync_WithMaxItems_LimitsResults()
    {
        // Arrange
        var maxItems = 2;

        var mockIterator = new Mock<FeedIterator<JobDocument>>();
        mockIterator.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);

        var mockFeedResponse = new Mock<FeedResponse<JobDocument>>();
        mockFeedResponse.Setup(r => r.GetEnumerator()).Returns(new List<JobDocument>().GetEnumerator());
        mockFeedResponse.Setup(r => r.RequestCharge).Returns(1.0);

        mockIterator
            .Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        _mockContainer
            .Setup(c => c.GetItemQueryIterator<JobDocument>(
                It.IsAny<QueryDefinition>(),
                null,
                It.Is<QueryRequestOptions>(opts => opts.MaxItemCount == maxItems)))
            .Returns(mockIterator.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        _ = await repository.GetAllJobsAsync(maxItems);

        // Assert
        _mockContainer.Verify(
            c => c.GetItemQueryIterator<JobDocument>(
                It.IsAny<QueryDefinition>(),
                null,
                It.Is<QueryRequestOptions>(opts => opts.MaxItemCount == maxItems)),
            Times.Once);
    }

    [Fact]
    public async Task GetAllJobsAsync_WithInvalidMaxItems_ThrowsArgumentException()
    {
        // Arrange
        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAllJobsAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAllJobsAsync(-1));
    }

    [Fact]
    public async Task GetAllJobsAsync_WithNoJobs_ReturnsEmptyList()
    {
        // Arrange
        var mockIterator = new Mock<FeedIterator<JobDocument>>();
        mockIterator.Setup(i => i.HasMoreResults).Returns(false);

        _mockContainer
            .Setup(c => c.GetItemQueryIterator<JobDocument>(
                It.IsAny<QueryDefinition>(),
                null,
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.GetAllJobsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateJobAsync_WithValidData_CreatesJobSuccessfully()
    {
        // Arrange
        var jobId = "new-job-123";
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

        var createdJob = new JobDocument
        {
            Id = jobId,
            JobId = jobId,
            Status = JobStatus.Pending,
            AudioFiles = audioFiles,
            CreatedAt = DateTime.UtcNow
        };

        var mockResponse = new Mock<ItemResponse<JobDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(createdJob);

        _mockContainer
            .Setup(c => c.CreateItemAsync(
                It.Is<JobDocument>(j => 
                    j.JobId == jobId && 
                    j.Status == JobStatus.Pending &&
                    j.AudioFiles == audioFiles),
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act
        var result = await repository.CreateJobAsync(jobId, audioFiles);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Equal(audioFiles, result.AudioFiles);
        
        _mockContainer.Verify(
            c => c.CreateItemAsync(
                It.IsAny<JobDocument>(),
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateJobAsync_WithInvalidJobId_ThrowsArgumentException(string? invalidJobId)
    {
        // Arrange
        var audioFiles = new[]
        {
            new AudioFileInfo
            {
                FileId = "file-001",
                BlobUrl = "https://storage.blob.core.windows.net/audio/file1.wav",
                FileName = "file1.wav"
            }
        };

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.CreateJobAsync(invalidJobId!, audioFiles));
    }

    [Fact]
    public async Task CreateJobAsync_WithNullAudioFiles_ThrowsArgumentException()
    {
        // Arrange
        var jobId = "test-job-123";
        
        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.CreateJobAsync(jobId, null!));
    }

    [Fact]
    public async Task CreateJobAsync_WithEmptyAudioFiles_ThrowsArgumentException()
    {
        // Arrange
        var jobId = "test-job-123";
        var audioFiles = Array.Empty<AudioFileInfo>();
        
        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.CreateJobAsync(jobId, audioFiles));
    }

    [Fact]
    public async Task CreateJobAsync_WithDuplicateJobId_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "duplicate-job-123";
        var audioFiles = new[]
        {
            new AudioFileInfo
            {
                FileId = $"{jobId}-001",
                BlobUrl = "https://storage.blob.core.windows.net/audio/file1.wav",
                FileName = "file1.wav"
            }
        };

        _mockContainer
            .Setup(c => c.CreateItemAsync(
                It.IsAny<JobDocument>(),
                It.IsAny<PartitionKey>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Conflict", System.Net.HttpStatusCode.Conflict, 0, "", 0));

        var repository = new CosmosDbJobRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.CreateJobAsync(jobId, audioFiles));
        
        Assert.Contains(jobId, exception.Message);
        Assert.Contains("already exists", exception.Message);
    }
}

