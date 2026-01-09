using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Cosmos DB implementation of Job Repository
/// </summary>
public class CosmosDbJobRepository : IJobRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbJobRepository> _logger;
    
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

    public CosmosDbJobRepository(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbJobRepository> logger)
    {
        _logger = logger;
        
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "TranscriptionDb";
        var containerName = configuration["CosmosDb:JobsContainerName"] ?? "Jobs";
        
        _container = cosmosClient.GetContainer(databaseName, containerName);
        
        _logger.LogInformation(
            "CosmosDbJobRepository initialized with Database: {DatabaseName}, Container: {ContainerName}",
            databaseName,
            containerName);
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
    /// This method performs a read-modify-write operation which requires two round trips to Cosmos DB.
    /// Performance implications:
    /// - 1 RU for the read operation (point read)
    /// - Varies for the write operation based on document size (typically 5-15 RUs)
    /// - Uses optimistic concurrency control with ETags to prevent lost updates
    /// - In high-throughput scenarios, consider the RU cost and potential retry overhead
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
            // Read the current job document with ETag for optimistic concurrency
            var response = await _container.ReadItemAsync<JobDocument>(
                jobId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            var job = response.Resource;
            var currentStatus = job.Status;
            var etag = response.ETag;

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
            job.Status = status;

            // Set startedAt when transitioning to Processing (only if not already set)
            if (status == JobStatus.Processing && startedAt.HasValue && !job.StartedAt.HasValue)
            {
                job.StartedAt = startedAt.Value;
                _logger.LogInformation(
                    "Setting StartedAt to {StartedAt} for JobId: {JobId}",
                    startedAt.Value,
                    jobId);
            }
            else if (status == JobStatus.Processing && job.StartedAt.HasValue)
            {
                _logger.LogInformation(
                    "StartedAt already set to {StartedAt} for JobId: {JobId}, preserving original value",
                    job.StartedAt.Value,
                    jobId);
            }

            // Set finishedAt when transitioning to final states (only if not already set)
            if (IsTerminalStatus(status) && finishedAt.HasValue && !job.FinishedAt.HasValue)
            {
                job.FinishedAt = finishedAt.Value;
                _logger.LogInformation(
                    "Setting FinishedAt to {FinishedAt} for JobId: {JobId}",
                    finishedAt.Value,
                    jobId);
            }
            else if (IsTerminalStatus(status) && job.FinishedAt.HasValue)
            {
                _logger.LogInformation(
                    "FinishedAt already set to {FinishedAt} for JobId: {JobId}, preserving original value",
                    job.FinishedAt.Value,
                    jobId);
            }

            // Replace the document in Cosmos DB with optimistic concurrency control using ETag
            var requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = etag
            };

            await _container.ReplaceItemAsync(
                job,
                jobId,
                new PartitionKey(jobId),
                requestOptions,
                cancellationToken);

            _logger.LogInformation(
                "Job status updated successfully for JobId: {JobId}",
                jobId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning(
                ex,
                "Concurrent update detected for JobId: {JobId}. The job was modified by another process.",
                jobId);
            throw new InvalidOperationException(
                $"Job with ID {jobId} was modified by another process. Please retry the operation.", ex);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
            var response = await _container.ReadItemAsync<JobDocument>(
                jobId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
            // Query all documents ordered by createdAt descending
            // Select only required fields to optimize data transfer and query performance
            // NOTE: For optimal performance, ensure the Cosmos DB container has a composite (or range) index
            // that includes /createdAt in descending order, e.g. in the indexing policy:
            // "compositeIndexes": [ [ { "path": "/createdAt", "order": "descending" } ] ]
            var query = new QueryDefinition("SELECT c.id, c.jobId, c.status, c.createdAt FROM c ORDER BY c.createdAt DESC");
            
            var queryRequestOptions = new QueryRequestOptions
            {
                MaxItemCount = maxItems
            };

            var iterator = _container.GetItemQueryIterator<JobDocument>(query, requestOptions: queryRequestOptions);
            var results = new List<JobDocument>();

            // Fetch only the first page to respect maxItems
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
                
                _logger.LogInformation(
                    "Retrieved {Count} jobs (RU charge: {RequestCharge})",
                    results.Count,
                    response.RequestCharge);
            }

            // Safety check: ensure we don't return more items than requested
            return results.Take(maxItems).ToList();
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

            var response = await _container.CreateItemAsync(
                jobDocument,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Job created successfully with JobId: {JobId}, Status: {Status}, AudioFiles: {AudioFileCount}",
                jobId,
                JobStatus.Pending,
                audioFiles.Length);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
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
