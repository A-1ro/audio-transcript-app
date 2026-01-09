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
/// TableStorageTranscriptionRepositoryのテスト
/// </summary>
public class TableStorageTranscriptionRepositoryTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<TableStorageTranscriptionRepository>> _mockLogger;
    private readonly TableStorageTranscriptionRepository _repository;

    public TableStorageTranscriptionRepositoryTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<TableStorageTranscriptionRepository>>();

        // Configurationのモック設定
        _mockConfiguration.Setup(c => c["TableStorage:TranscriptionsTableName"]).Returns("Transcriptions");

        // TableServiceClientのモック設定
        _mockTableServiceClient
            .Setup(c => c.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);

        _repository = new TableStorageTranscriptionRepository(
            _mockTableServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetTranscriptionAsync_WhenExists_ReturnsDocument()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var entity = new TranscriptionEntity
        {
            PartitionKey = jobId,
            RowKey = fileId,
            JobId = jobId,
            FileId = fileId,
            TranscriptText = "Test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };

        var mockResponse = Response.FromValue(entity, Mock.Of<Response>());

        _mockTableClient
            .Setup(c => c.GetEntityAsync<TranscriptionEntity>(
                jobId,
                fileId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _repository.GetTranscriptionAsync(jobId, fileId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(fileId, result.FileId);
        Assert.Equal("Test transcription", result.TranscriptText);
    }

    [Fact]
    public async Task GetTranscriptionAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";

        _mockTableClient
            .Setup(c => c.GetEntityAsync<TranscriptionEntity>(
                jobId,
                fileId,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act
        var result = await _repository.GetTranscriptionAsync(jobId, fileId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_NewDocument_CreatesSuccessfully()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var transcriptText = "New transcription";
        var confidence = 0.95;
        var status = TranscriptionStatus.Completed;

        // Mock GetEntityAsync to throw 404 (document doesn't exist)
        _mockTableClient
            .Setup(c => c.GetEntityAsync<TranscriptionEntity>(
                jobId,
                fileId,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Mock UpsertEntityAsync
        _mockTableClient
            .Setup(c => c.UpsertEntityAsync(
                It.IsAny<TranscriptionEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        var result = await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            transcriptText,
            confidence,
            status);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(fileId, result.FileId);
        Assert.Equal(transcriptText, result.TranscriptText);
        Assert.Equal(confidence, result.Confidence);
        Assert.Equal(status, result.Status);

        // Verify UpsertEntityAsync was called
        _mockTableClient.Verify(
            c => c.UpsertEntityAsync(
                It.Is<TranscriptionEntity>(e =>
                    e.JobId == jobId &&
                    e.FileId == fileId &&
                    e.TranscriptText == transcriptText),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_ExistingDocument_PreservesCreatedAt()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var existingCreatedAt = DateTime.UtcNow.AddHours(-1);
        var existingEntity = new TranscriptionEntity
        {
            PartitionKey = jobId,
            RowKey = fileId,
            JobId = jobId,
            FileId = fileId,
            CreatedAt = existingCreatedAt
        };

        var mockGetResponse = Response.FromValue(existingEntity, Mock.Of<Response>());

        _mockTableClient
            .Setup(c => c.GetEntityAsync<TranscriptionEntity>(
                jobId,
                fileId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGetResponse);

        _mockTableClient
            .Setup(c => c.UpsertEntityAsync(
                It.IsAny<TranscriptionEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        var result = await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            "Updated text",
            0.90,
            TranscriptionStatus.Completed);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingCreatedAt, result.CreatedAt);

        // Verify UpsertEntityAsync was called with preserved CreatedAt
        _mockTableClient.Verify(
            c => c.UpsertEntityAsync(
                It.Is<TranscriptionEntity>(e => e.CreatedAt == existingCreatedAt),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_CompletedWithoutText_ThrowsException()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.SaveTranscriptionAsync(
                jobId,
                fileId,
                null, // No transcript text
                0.95,
                TranscriptionStatus.Completed));
    }
}
