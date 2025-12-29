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
            // Read the current job document
            var response = await _container.ReadItemAsync<JobDocument>(
                jobId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            var job = response.Resource;
            var currentStatus = job.Status;

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

            // Set startedAt when transitioning to Processing
            if (status == JobStatus.Processing && startedAt.HasValue)
            {
                job.StartedAt = startedAt.Value;
                _logger.LogInformation(
                    "Setting StartedAt to {StartedAt} for JobId: {JobId}",
                    startedAt.Value,
                    jobId);
            }

            // Set finishedAt when transitioning to final states
            if ((status == JobStatus.Completed || status == JobStatus.Failed || status == JobStatus.PartiallyFailed)
                && finishedAt.HasValue)
            {
                job.FinishedAt = finishedAt.Value;
                _logger.LogInformation(
                    "Setting FinishedAt to {FinishedAt} for JobId: {JobId}",
                    finishedAt.Value,
                    jobId);
            }

            // Replace the document in Cosmos DB
            await _container.ReplaceItemAsync(
                job,
                jobId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Job status updated successfully for JobId: {JobId}",
                jobId);
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
}
