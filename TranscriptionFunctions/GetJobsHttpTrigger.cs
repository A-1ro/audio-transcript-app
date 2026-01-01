using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using TranscriptionFunctions.Services;

namespace TranscriptionFunctions;

/// <summary>
/// HTTP Trigger for getting all jobs
/// </summary>
public class GetJobsHttpTrigger
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<GetJobsHttpTrigger> _logger;

    public GetJobsHttpTrigger(
        IJobRepository jobRepository,
        ILogger<GetJobsHttpTrigger> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all jobs endpoint
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>List of jobs</returns>
    [Function("GetJobs")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GET /api/jobs request");

        try
        {
            // Get maxItems from query parameter (default: 100)
            var maxItems = 100;
            if (req.Query["maxItems"] is string maxItemsStr && int.TryParse(maxItemsStr, out var parsedMaxItems))
            {
                maxItems = parsedMaxItems;
            }

            // Fetch jobs from repository
            var jobs = await _jobRepository.GetAllJobsAsync(maxItems);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            // Serialize and write jobs
            var jobList = jobs.Select(j => new
            {
                jobId = j.JobId,
                status = j.Status,
                createdAt = j.CreatedAt.ToString("o") // ISO 8601 format
            }).ToList();

            await response.WriteStringAsync(JsonSerializer.Serialize(jobList));

            _logger.LogInformation("Successfully returned {Count} jobs", jobList.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process GET /api/jobs request");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Failed to fetch jobs",
                message = ex.Message
            }));

            return errorResponse;
        }
    }
}
