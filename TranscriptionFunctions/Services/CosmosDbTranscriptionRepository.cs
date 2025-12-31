using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Cosmos DB implementation of Transcription Repository
/// </summary>
public class CosmosDbTranscriptionRepository : ITranscriptionRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbTranscriptionRepository> _logger;

    public CosmosDbTranscriptionRepository(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbTranscriptionRepository> logger)
    {
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "TranscriptionDb";
        var containerName = configuration["CosmosDb:TranscriptionsContainerName"] ?? "Transcriptions";

        _container = cosmosClient.GetContainer(databaseName, containerName);

        _logger.LogInformation(
            "CosmosDbTranscriptionRepository initialized with Database: {DatabaseName}, Container: {ContainerName}",
            databaseName,
            containerName);
    }

    /// <summary>
    /// Create a unique document ID from JobId and FileId
    /// </summary>
    private static string CreateDocumentId(string jobId, string fileId)
    {
        return $"{jobId}_{fileId}";
    }

    /// <summary>
    /// Get transcription result by JobId and FileId
    /// </summary>
    public async Task<TranscriptionDocument?> GetTranscriptionAsync(
        string jobId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ArgumentException("FileId cannot be null or empty", nameof(fileId));
        }

        var documentId = CreateDocumentId(jobId, fileId);

        try
        {
            var response = await _container.ReadItemAsync<TranscriptionDocument>(
                documentId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Found existing transcription for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
                "No existing transcription found for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
            return null;
        }
    }

    /// <summary>
    /// Save or update transcription result (idempotent using Upsert)
    /// </summary>
    public async Task<TranscriptionDocument> SaveTranscriptionAsync(
        string jobId,
        string fileId,
        string? transcriptText,
        double confidence,
        string status,
        string? rawResult = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ArgumentException("FileId cannot be null or empty", nameof(fileId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        // TranscriptText is required when transcription is successful
        if (status == TranscriptionStatus.Completed && string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new ArgumentException(
                "TranscriptText cannot be null or empty when Status is Completed",
                nameof(transcriptText));
        }

        var documentId = CreateDocumentId(jobId, fileId);

        // Note: Using DateTime.UtcNow for CreatedAt. This is acceptable here as:
        // 1. Cosmos DB also manages its own _ts timestamp field
        // 2. CreatedAt is informational and doesn't affect business logic
        // 3. For testability improvements, consider injecting IDateTimeProvider in the future
        var document = new TranscriptionDocument
        {
            Id = documentId,
            JobId = jobId,
            FileId = fileId,
            TranscriptText = transcriptText,
            Confidence = confidence,
            Status = status,
            RawResult = rawResult,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Upserting transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
                jobId,
                fileId,
                status);

            // Upsert operation ensures idempotency - if document exists, it will be updated
            var response = await _container.UpsertItemAsync(
                document,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Transcription result saved successfully for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);

            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save transcription result for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
            
            // Re-throw with additional context to aid debugging
            throw new InvalidOperationException(
                $"Failed to save transcription result for JobId: {jobId}, FileId: {fileId}. Document ID: {documentId}",
                ex);
        }
    }
}
