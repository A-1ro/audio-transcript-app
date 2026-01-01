using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests.Services;

/// <summary>
/// CosmosDbTranscriptionRepositoryのテスト
/// </summary>
public class CosmosDbTranscriptionRepositoryTests
{
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<Container> _mockContainer;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CosmosDbTranscriptionRepository>> _mockLogger;
    private readonly CosmosDbTranscriptionRepository _repository;

    public CosmosDbTranscriptionRepositoryTests()
    {
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockContainer = new Mock<Container>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CosmosDbTranscriptionRepository>>();

        // Configurationのモック設定
        _mockConfiguration.Setup(c => c["CosmosDb:DatabaseName"]).Returns("TranscriptionDb");
        _mockConfiguration.Setup(c => c["CosmosDb:TranscriptionsContainerName"]).Returns("Transcriptions");

        // CosmosClientのモック設定
        _mockCosmosClient
            .Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_mockContainer.Object);

        _repository = new CosmosDbTranscriptionRepository(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetTranscriptionAsync_WhenExists_ReturnsDocument()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var documentId = $"{jobId}_{fileId}";
        var expectedDocument = new TranscriptionDocument
        {
            Id = documentId,
            JobId = jobId,
            FileId = fileId,
            TranscriptText = "Test transcription",
            Confidence = 0.95,
            Status = TranscriptionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        var mockResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(expectedDocument);

        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                documentId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _repository.GetTranscriptionAsync(jobId, fileId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(documentId, result.Id);
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
        var documentId = $"{jobId}_{fileId}";

        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                documentId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        // Act
        var result = await _repository.GetTranscriptionAsync(jobId, fileId);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "file-001")]
    [InlineData(null, "file-001")]
    [InlineData("job-123", "")]
    [InlineData("job-123", null)]
    public async Task GetTranscriptionAsync_WithInvalidInput_ThrowsArgumentException(
        string? jobId,
        string? fileId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.GetTranscriptionAsync(jobId!, fileId!));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithValidInput_SavesDocument()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var transcriptText = "Test transcription";
        var confidence = 0.95;
        var status = TranscriptionStatus.Completed;

        // Mock ReadItemAsync to throw NotFound (document doesn't exist yet)
        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        var mockResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(
            It.IsAny<TranscriptionDocument>());

        _mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<TranscriptionDocument>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            transcriptText,
            confidence,
            status);

        // Assert
        _mockContainer.Verify(
            c => c.UpsertItemAsync(
                It.Is<TranscriptionDocument>(d =>
                    d.Id == $"{jobId}_{fileId}" &&
                    d.JobId == jobId &&
                    d.FileId == fileId &&
                    d.TranscriptText == transcriptText &&
                    Math.Abs(d.Confidence - confidence) < 0.0001 &&
                    d.Status == status),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", "file-001", "text", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-123", "", "text", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-123", "file-001", "text", 0.95, "")]
    [InlineData("job-123", "file-001", "", 0.95, TranscriptionStatus.Completed)]
    [InlineData("job-123", "file-001", null, 0.95, TranscriptionStatus.Completed)]
    public async Task SaveTranscriptionAsync_WithInvalidInput_ThrowsArgumentException(
        string jobId,
        string fileId,
        string? transcriptText,
        double confidence,
        string status)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.SaveTranscriptionAsync(
                jobId,
                fileId,
                transcriptText,
                confidence,
                status));
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithFailedStatus_AllowsEmptyTranscriptText()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var transcriptText = "";
        var confidence = 0.0;
        var status = TranscriptionStatus.Failed;

        // Mock ReadItemAsync to throw NotFound (document doesn't exist yet)
        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        var mockResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(
            It.IsAny<TranscriptionDocument>());

        _mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<TranscriptionDocument>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            transcriptText,
            confidence,
            status);

        // Assert - Should not throw
        _mockContainer.Verify(
            c => c.UpsertItemAsync(
                It.IsAny<TranscriptionDocument>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithRawResult_SavesRawResult()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var transcriptText = "Test";
        var confidence = 0.95;
        var status = TranscriptionStatus.Completed;
        var rawResult = "{\"raw\":\"data\"}";

        // Mock ReadItemAsync to throw NotFound (document doesn't exist yet)
        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        var mockResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(
            It.IsAny<TranscriptionDocument>());

        _mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<TranscriptionDocument>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            transcriptText,
            confidence,
            status,
            rawResult);

        // Assert
        _mockContainer.Verify(
            c => c.UpsertItemAsync(
                It.Is<TranscriptionDocument>(d =>
                    d.RawResult == rawResult),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WhenDocumentExists_PreservesCreatedAt()
    {
        // Arrange
        var jobId = "job-123";
        var fileId = "file-001";
        var transcriptText = "Test";
        var confidence = 0.95;
        var status = TranscriptionStatus.Completed;
        var originalCreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var existingDocument = new TranscriptionDocument
        {
            Id = $"{jobId}_{fileId}",
            JobId = jobId,
            FileId = fileId,
            TranscriptText = "Old text",
            Confidence = 0.8,
            Status = TranscriptionStatus.Completed,
            CreatedAt = originalCreatedAt
        };

        var mockReadResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockReadResponse.Setup(r => r.Resource).Returns(existingDocument);

        // Mock ReadItemAsync to return existing document
        _mockContainer
            .Setup(c => c.ReadItemAsync<TranscriptionDocument>(
                $"{jobId}_{fileId}",
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReadResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<TranscriptionDocument>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(
            It.IsAny<TranscriptionDocument>());

        _mockContainer
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<TranscriptionDocument>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        await _repository.SaveTranscriptionAsync(
            jobId,
            fileId,
            transcriptText,
            confidence,
            status);

        // Assert - CreatedAt should be preserved from original document
        _mockContainer.Verify(
            c => c.UpsertItemAsync(
                It.Is<TranscriptionDocument>(d =>
                    d.CreatedAt == originalCreatedAt &&
                    d.TranscriptText == transcriptText &&
                    Math.Abs(d.Confidence - confidence) < 0.0001),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
