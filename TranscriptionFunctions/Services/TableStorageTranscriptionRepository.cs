using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Models.TableEntities;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Azure Table Storage implementation of Transcription Repository
/// </summary>
public class TableStorageTranscriptionRepository : ITranscriptionRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageTranscriptionRepository> _logger;

    public TableStorageTranscriptionRepository(
        TableServiceClient tableServiceClient,
        IConfiguration configuration,
        ILogger<TableStorageTranscriptionRepository> _logger)
    {
        this._logger = _logger;

        var tableName = configuration["TableStorage:TranscriptionsTableName"] ?? "Transcriptions";
        _tableClient = tableServiceClient.GetTableClient(tableName);

        // Ensure table exists (idempotent operation)
        _tableClient.CreateIfNotExists();

        _logger.LogInformation(
            "TableStorageTranscriptionRepository initialized with Table: {TableName}",
            tableName);
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

        try
        {
            var response = await _tableClient.GetEntityAsync<TranscriptionEntity>(
                jobId, // PartitionKey
                fileId, // RowKey
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Found existing transcription for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);

            return response.Value.ToDocument();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug(
                "No existing transcription found for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
            return null;
        }
    }

    /// <summary>
    /// Save or update transcription result (idempotent using UpsertEntity)
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

        // Check if document already exists to preserve CreatedAt timestamp
        // Design tradeoff: This performs an extra read operation before every upsert.
        // For new documents, this results in a NotFound response (extra cost), but it's necessary
        // to preserve CreatedAt for existing documents and maintain true idempotency.
        // Alternative approaches (e.g., conditional upsert with ETag) would be more complex
        // and this prioritizes correctness and simplicity over performance.
        DateTime createdAt;
        try
        {
            var existingEntity = await _tableClient.GetEntityAsync<TranscriptionEntity>(
                jobId, // PartitionKey
                fileId, // RowKey
                cancellationToken: cancellationToken);
            
            // Preserve original CreatedAt for true idempotency
            createdAt = existingEntity.Value.CreatedAt;
            
            _logger.LogDebug(
                "Updating existing transcription for JobId: {JobId}, FileId: {FileId}, preserving CreatedAt: {CreatedAt}",
                jobId,
                fileId,
                createdAt);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Document doesn't exist, use current time
            createdAt = DateTime.UtcNow;
            
            _logger.LogDebug(
                "Creating new transcription for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
        }

        var document = new TranscriptionDocument
        {
            Id = $"{jobId}_{fileId}",
            JobId = jobId,
            FileId = fileId,
            TranscriptText = transcriptText,
            Confidence = confidence,
            Status = status,
            RawResult = rawResult,
            CreatedAt = createdAt
        };

        var entity = TranscriptionEntity.FromDocument(document);

        try
        {
            _logger.LogInformation(
                "Upserting transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
                jobId,
                fileId,
                status);

            // UpsertEntity ensures idempotency - if entity exists, it will be updated
            await _tableClient.UpsertEntityAsync(
                entity,
                TableUpdateMode.Replace, // Replace mode for full update
                cancellationToken);

            _logger.LogInformation(
                "Transcription result saved successfully for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save transcription result for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
            
            // Re-throw original exception to preserve exception type information
            // Context is already logged above
            throw;
        }
    }
}
