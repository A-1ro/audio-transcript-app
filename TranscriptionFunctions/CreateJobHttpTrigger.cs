using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using TranscriptionFunctions.Constants;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions;

/// <summary>
/// HTTP Trigger for creating a new transcription job
/// </summary>
public class CreateJobHttpTrigger
{
    private readonly IJobRepository _jobRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateJobHttpTrigger> _logger;

    public CreateJobHttpTrigger(
        IJobRepository jobRepository,
        IConfiguration configuration,
        ILogger<CreateJobHttpTrigger> logger)
    {
        _jobRepository = jobRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Create a new job endpoint
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>Created job information</returns>
    [Function("CreateJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs")] HttpRequestData req)
    {
        _logger.LogInformation("Processing POST /api/jobs request");

        try
        {
            // Parse request body
            CreateJobRequest? requestBody;
            try
            {
                requestBody = await req.ReadFromJsonAsync<CreateJobRequest>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in request body");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request format",
                    message = "Request body must be valid JSON"
                }));
                return errorResponse;
            }

            // Validate request body
            if (requestBody == null || requestBody.AudioFiles == null || requestBody.AudioFiles.Length == 0)
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                validationResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await validationResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "audioFiles array is required and must not be empty"
                }));
                return validationResponse;
            }

            // Validate each audio file
            for (int i = 0; i < requestBody.AudioFiles.Length; i++)
            {
                var audioFile = requestBody.AudioFiles[i];
                if (string.IsNullOrWhiteSpace(audioFile.FileName))
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    validationResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await validationResponse.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        error = "Invalid request",
                        message = $"audioFiles[{i}].fileName is required and must not be empty"
                    }));
                    return validationResponse;
                }

                if (string.IsNullOrWhiteSpace(audioFile.BlobUrl))
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    validationResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await validationResponse.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        error = "Invalid request",
                        message = $"audioFiles[{i}].blobUrl is required and must not be empty"
                    }));
                    return validationResponse;
                }

                // Validate blobUrl is a valid URL
                if (!Uri.TryCreate(audioFile.BlobUrl, UriKind.Absolute, out _))
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    validationResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await validationResponse.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        error = "Invalid request",
                        message = $"audioFiles[{i}].blobUrl must be a valid absolute URL"
                    }));
                    return validationResponse;
                }
            }

            // Generate new Job ID
            var jobId = Guid.NewGuid().ToString();

            // Check queue configuration before creating the job to avoid orphaned jobs
            var queueConnectionString = _configuration["AzureWebJobsStorage"];
            if (string.IsNullOrWhiteSpace(queueConnectionString))
            {
                _logger.LogError("AzureWebJobsStorage connection string is not configured");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Server configuration error",
                    message = "Queue storage is not configured"
                }));
                return errorResponse;
            }

            // Convert request audio files to AudioFileInfo model
            var audioFileInfos = requestBody.AudioFiles.Select((af, index) => new Models.AudioFileInfo
            {
                FileId = $"{jobId}-{index:D3}",
                BlobUrl = af.BlobUrl,
                FileName = af.FileName
            }).ToArray();

            // Create job in Cosmos DB
            var job = await _jobRepository.CreateJobAsync(jobId, audioFileInfos);

            // Enqueue job to Azure Queue Storage
            try
            {
                var queueClient = new QueueClient(queueConnectionString, QueueNames.TranscriptionJobs);
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(jobId);

                _logger.LogInformation(
                    "Job created and enqueued successfully. JobId: {JobId}, AudioFiles: {AudioFileCount}",
                    jobId,
                    requestBody.AudioFiles.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to enqueue job message for JobId: {JobId}",
                    jobId);
                // Job is created in DB but not enqueued - this is a partial failure
                // The job will remain in Pending state and won't be processed
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Failed to enqueue job",
                    message = "Job was created but could not be queued for processing",
                    jobId = jobId
                }));
                return errorResponse;
            }

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var responseBody = new
            {
                jobId = job.JobId,
                status = job.Status
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));

            return response;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            _logger.LogWarning(ex, "Attempted to create a job that already exists");
            var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
            conflictResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await conflictResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Job already exists",
                message = ex.Message
            }));
            return conflictResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process POST /api/jobs request");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Failed to create job",
                message = "An internal error occurred while processing the request."
            }));

            return errorResponse;
        }
    }
}

/// <summary>
/// Request body for creating a job
/// </summary>
public record CreateJobRequest
{
    public required AudioFileRequest[] AudioFiles { get; init; }
}

/// <summary>
/// Audio file information in the request
/// </summary>
public record AudioFileRequest
{
    public required string FileName { get; init; }
    public required string BlobUrl { get; init; }
}
