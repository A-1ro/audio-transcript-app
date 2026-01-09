using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Models.TableEntities;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Azure Table Storage implementation of Job Repository
/// </summary>
public class TableStorageJobRepository : IJobRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageJobRepository> _logger;
    
    // Valid state transitions
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        [JobStatus.Pending] = new HashSet<string> { JobStatus.Processing, JobStatus.Failed },
        [JobStatus.Processing] = new HashSet<string> { JobStatus.Completed, JobStatus.Failed, JobStatus.PartiallyFailed },
        [JobStatus.Completed] = new HashSet<string>(),
        [JobStatus.Failed] = new HashSet<string>(),
        [JobStatus.PartiallyFailed] = new HashSet<string>()
    };

    // Terminal status values
    private static readonly HashSet<string> TerminalStatuses = new()
    {
        JobStatus.Completed,
        JobStatus.Failed,
        JobStatus.PartiallyFailed
    };

    public TableStorageJobRepository(
        TableServiceClient tableServiceClient,
        IConfiguration configuration,
        ILogger<TableStorageJobRepository> logger)
    {
        _logger = logger;
        
        var tableName = configuration["TableStorage:JobsTableName"] ?? "Jobs";
        _tableClient = tableServiceClient.GetTableClient(tableName);
        
        // Ensure table exists (idempotent operation)
        _tableClient.CreateIfNotExists();
        
        _logger.LogInformation(
            "TableStorageJobRepository initialized with Table: {TableName}",
            tableName);
    }

    /// <summary>
    /// Update job status and timestamps
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="status">New status</param>
    /// <param name="startedAt">Started timestamp (optional)</param>
    /// <param name="finishedAt">Finished timestamp (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// This method performs a read-modify-write operation which requires two round trips to Table Storage.
    /// Performance implications:
    /// - Read operation (point read by partition key and row key)
    /// - Write operation with ETag for optimistic concurrency control
    /// - In high-throughput scenarios, consider the cost and potential retry overhead
    /// </remarks>
    public async Task UpdateJobStatusAsync(
        string jobId,
        string status,
        DateTime? startedAt = null,
        DateTime? finishedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        _logger.LogInformation(
            "Updating job status for JobId: {JobId} to {Status}",
            jobId,
            status);

        try
        {
            // Read the current job entity with ETag for optimistic concurrency
            var response = await _tableClient.GetEntityAsync<JobEntity>(
                jobId, // PartitionKey
                jobId, // RowKey
                cancellationToken: cancellationToken);

            var jobEntity = response.Value;
            var currentStatus = jobEntity.Status;
            var etag = jobEntity.ETag;

            // Validate state transition
            if (!IsValidTransition(currentStatus, status))
            {
                var errorMessage = $"Invalid state transition from {currentStatus} to {status} for JobId: {jobId}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation(
                "Valid state transition from {CurrentStatus} to {NewStatus} for JobId: {JobId}",
                currentStatus,
                status,
                jobId);

            // Update job fields
            jobEntity.Status = status;

            // Set startedAt when transitioning to Processing (only if not already set)
            if (status == JobStatus.Processing && startedAt.HasValue && !jobEntity.StartedAt.HasValue)
            {
                jobEntity.StartedAt = startedAt.Value;
                _logger.LogInformation(
                    "Setting StartedAt to {StartedAt} for JobId: {JobId}",
                    startedAt.Value,
                    jobId);
            }
            else if (status == JobStatus.Processing && jobEntity.StartedAt.HasValue)
            {
                _logger.LogInformation(
                    "StartedAt already set to {StartedAt} for JobId: {JobId}, preserving original value",
                    jobEntity.StartedAt.Value,
                    jobId);
            }

            // Set finishedAt when transitioning to final states (only if not already set)
            if (IsTerminalStatus(status) && finishedAt.HasValue && !jobEntity.FinishedAt.HasValue)
            {
                jobEntity.FinishedAt = finishedAt.Value;
                _logger.LogInformation(
                    "Setting FinishedAt to {FinishedAt} for JobId: {JobId}",
                    finishedAt.Value,
                    jobId);
            }
            else if (IsTerminalStatus(status) && jobEntity.FinishedAt.HasValue)
            {
                _logger.LogInformation(
                    "FinishedAt already set to {FinishedAt} for JobId: {JobId}, preserving original value",
                    jobEntity.FinishedAt.Value,
                    jobId);
            }

            // Update the entity in Table Storage with optimistic concurrency control using ETag
            await _tableClient.UpdateEntityAsync(
                jobEntity,
                etag,
                TableUpdateMode.Replace,
                cancellationToken);

            _logger.LogInformation(
                "Job status updated successfully for JobId: {JobId}",
                jobId);
        }
        catch (RequestFailedException ex) when (ex.Status == 412) // Precondition Failed
        {
            _logger.LogWarning(
                ex,
                "Concurrent update detected for JobId: {JobId}. The job was modified by another process.",
                jobId);
            throw new InvalidOperationException(
                $"Job with ID {jobId} was modified by another process. Please retry the operation.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError(
                ex,
                "Job not found for JobId: {JobId}",
                jobId);
            throw new InvalidOperationException($"Job with ID {jobId} not found", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update job status for JobId: {JobId}",
                jobId);
            throw;
        }
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job document or null if not found</returns>
    /// <remarks>
    /// This method returns null when a job is not found, whereas UpdateJobStatusAsync throws InvalidOperationException.
    /// This is intentional: GetJobAsync is used for queries where a missing job is a valid scenario,
    /// while UpdateJobStatusAsync expects the job to exist and treats missing jobs as an error condition.
    /// </remarks>
    public async Task<JobDocument?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        try
        {
            var response = await _tableClient.GetEntityAsync<JobEntity>(
                jobId, // PartitionKey
                jobId, // RowKey
                cancellationToken: cancellationToken);

            return response.Value.ToDocument();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(
                "Job not found for JobId: {JobId}",
                jobId);
            return null;
        }
    }

    /// <summary>
    /// Validates if a state transition is allowed
    /// </summary>
    private static bool IsValidTransition(string currentStatus, string newStatus)
    {
        // If current status is the same as new status, allow it (idempotency)
        if (currentStatus == newStatus)
        {
            return true;
        }

        // Check if the transition is in the valid transitions map
        if (ValidTransitions.TryGetValue(currentStatus, out var allowedTransitions))
        {
            return allowedTransitions.Contains(newStatus);
        }

        // Unknown current status - reject transition
        return false;
    }

    /// <summary>
    /// Checks if a status is a terminal status (Completed, Failed, or PartiallyFailed)
    /// </summary>
    private static bool IsTerminalStatus(string status)
    {
        return TerminalStatuses.Contains(status);
    }

    /// <summary>
    /// Get all jobs ordered by creation date (descending)
    /// </summary>
    /// <param name="maxItems">Maximum number of items to return (default: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of job documents</returns>
    public async Task<IEnumerable<JobDocument>> GetAllJobsAsync(int maxItems = 100, CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            throw new ArgumentException("MaxItems must be greater than 0", nameof(maxItems));
        }

        _logger.LogInformation("Fetching all jobs with maxItems: {MaxItems}", maxItems);

        try
        {
            // Query all entities and sort by CreatedAt in memory
            // Note: Table Storage doesn't support server-side ordering, so we need to fetch and sort
            var entities = new List<JobEntity>();
            
            await foreach (var entity in _tableClient.QueryAsync<JobEntity>(cancellationToken: cancellationToken))
            {
                entities.Add(entity);
            }

            // Sort by CreatedAt descending and take maxItems
            var sortedEntities = entities
                .OrderByDescending(e => e.CreatedAt)
                .Take(maxItems);

            var results = sortedEntities.Select(e => e.ToDocument()).ToList();
            
            _logger.LogInformation(
                "Retrieved {Count} jobs",
                results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all jobs");
            throw;
        }
    }

    /// <summary>
    /// Create a new job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="audioFiles">Audio files associated with the job</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created job document</returns>
    public async Task<JobDocument> CreateJobAsync(string jobId, AudioFileInfo[] audioFiles, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        if (audioFiles == null || audioFiles.Length == 0)
        {
            throw new ArgumentException("AudioFiles cannot be null or empty", nameof(audioFiles));
        }

        _logger.LogInformation("Creating new job with JobId: {JobId}", jobId);

        try
        {
            var now = DateTime.UtcNow;
            var jobDocument = new JobDocument
            {
                Id = jobId,
                JobId = jobId,
                Status = JobStatus.Pending,
                AudioFiles = audioFiles,
                CreatedAt = now
            };

            var entity = JobEntity.FromDocument(jobDocument);

            await _tableClient.AddEntityAsync(entity, cancellationToken);

            _logger.LogInformation(
                "Job created successfully with JobId: {JobId}, Status: {Status}, AudioFiles: {AudioFileCount}",
                jobId,
                JobStatus.Pending,
                audioFiles.Length);

            return jobDocument;
        }
        catch (RequestFailedException ex) when (ex.Status == 409) // Conflict
        {
            _logger.LogError(
                ex,
                "Job with JobId: {JobId} already exists",
                jobId);
            throw new InvalidOperationException($"Job with ID {jobId} already exists", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create job with JobId: {JobId}",
                jobId);
            throw;
        }
    }
}
