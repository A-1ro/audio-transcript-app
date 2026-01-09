using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 音声ファイル一覧取得Activity
/// </summary>
public class GetAudioFilesActivity
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<GetAudioFilesActivity> _logger;

    public GetAudioFilesActivity(
        IJobRepository jobRepository,
        ILogger<GetAudioFilesActivity> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    /// <summary>
    /// ジョブに紐づく音声ファイル一覧を取得する
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <returns>音声ファイル一覧</returns>
    [Function(nameof(GetAudioFilesActivity))]
    public async Task<List<AudioFileInfo>> RunAsync([ActivityTrigger] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(jobId));
        }

        // ログスコープにJobIdを追加
        using (_logger.BeginScope(new Dictionary<string, object> { ["JobId"] = jobId }))
        {
            _logger.LogInformation("Getting audio files for JobId: {JobId}", jobId);

            // Get job document from Cosmos DB
            var job = await _jobRepository.GetJobAsync(jobId);
            
            if (job == null)
            {
                _logger.LogError("Job not found for JobId: {JobId}", jobId);
                throw new InvalidOperationException($"Job with ID {jobId} not found");
            }

            if (job.AudioFiles == null || job.AudioFiles.Length == 0)
            {
                _logger.LogWarning("No audio files found for JobId: {JobId}", jobId);
                return new List<AudioFileInfo>();
            }

            var audioFiles = job.AudioFiles.ToList();

            _logger.LogInformation("Found {Count} audio files for JobId: {JobId}", audioFiles.Count, jobId);

            return audioFiles;
        }
    }
}
