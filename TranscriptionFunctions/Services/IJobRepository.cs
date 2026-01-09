using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Services;

/// <summary>
/// Job Repository Interface
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Update job status and timestamps
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="status">New status</param>
    /// <param name="startedAt">Started timestamp (optional)</param>
    /// <param name="finishedAt">Finished timestamp (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateJobStatusAsync(
        string jobId, 
        string status, 
        DateTime? startedAt = null, 
        DateTime? finishedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get job by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job document or null if not found</returns>
    Task<JobDocument?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all jobs ordered by creation date (descending)
    /// </summary>
    /// <param name="maxItems">Maximum number of items to return (default: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of job documents</returns>
    Task<IEnumerable<JobDocument>> GetAllJobsAsync(int maxItems = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="audioFiles">Audio files associated with the job</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created job document</returns>
    Task<JobDocument> CreateJobAsync(string jobId, AudioFileInfo[] audioFiles, CancellationToken cancellationToken = default);
}
