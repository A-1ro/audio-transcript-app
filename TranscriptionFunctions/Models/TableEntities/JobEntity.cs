using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;

namespace TranscriptionFunctions.Models.TableEntities;

/// <summary>
/// Azure Table Storage entity for Jobs
/// PartitionKey: JobId
/// RowKey: JobId (same as PartitionKey for single-row partition)
/// </summary>
public class JobEntity : ITableEntity
{
    /// <summary>
    /// Partition Key (JobId for data partitioning)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row Key (JobId, same as PartitionKey)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp managed by Azure Table Storage
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Job Status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Audio files associated with this job (stored as JSON)
    /// </summary>
    public string? AudioFilesJson { get; set; }

    /// <summary>
    /// Job Created Timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Job Started Timestamp (when status changed to Processing)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Job Finished Timestamp (when status changed to Completed/Failed/PartiallyFailed)
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Convert from JobDocument
    /// </summary>
    public static JobEntity FromDocument(JobDocument document)
    {
        return new JobEntity
        {
            PartitionKey = document.JobId,
            RowKey = document.JobId,
            JobId = document.JobId,
            Status = document.Status,
            AudioFilesJson = document.AudioFiles != null 
                ? JsonConvert.SerializeObject(document.AudioFiles)
                : null,
            CreatedAt = document.CreatedAt,
            StartedAt = document.StartedAt,
            FinishedAt = document.FinishedAt
        };
    }

    /// <summary>
    /// Convert to JobDocument
    /// </summary>
    public JobDocument ToDocument()
    {
        return new JobDocument
        {
            Id = JobId,
            JobId = JobId,
            Status = Status,
            AudioFiles = !string.IsNullOrEmpty(AudioFilesJson)
                ? JsonConvert.DeserializeObject<AudioFileInfo[]>(AudioFilesJson)!
                : null,
            CreatedAt = CreatedAt,
            StartedAt = StartedAt,
            FinishedAt = FinishedAt,
            Timestamp = Timestamp?.ToUnixTimeSeconds()
        };
    }
}
