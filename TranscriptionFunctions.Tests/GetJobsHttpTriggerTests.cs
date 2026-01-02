using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;

namespace TranscriptionFunctions.Tests;

/// <summary>
/// GetJobsHttpTriggerのテスト
/// </summary>
public class GetJobsHttpTriggerTests
{
    private readonly Mock<IJobRepository> _mockJobRepository;
    private readonly Mock<ILogger<GetJobsHttpTrigger>> _mockLogger;
    private readonly Mock<FunctionContext> _mockFunctionContext;

    public GetJobsHttpTriggerTests()
    {
        _mockJobRepository = new Mock<IJobRepository>();
        _mockLogger = new Mock<ILogger<GetJobsHttpTrigger>>();
        _mockFunctionContext = new Mock<FunctionContext>();
    }

    private Mock<HttpRequestData> CreateMockRequest(string? maxItems = null)
    {
        var mockRequest = new Mock<HttpRequestData>(_mockFunctionContext.Object);
        mockRequest.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var mockResponse = new Mock<HttpResponseData>(_mockFunctionContext.Object);
            mockResponse.SetupProperty(r => r.StatusCode);
            mockResponse.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            // Use a writable MemoryStream for testing
            var memoryStream = new MemoryStream();
            mockResponse.SetupGet(r => r.Body).Returns(memoryStream);
            return mockResponse.Object;
        });

        // Setup query parameters using a custom mock
        var mockQuery = new MockQueryCollection();
        if (maxItems != null)
        {
            mockQuery.Add("maxItems", maxItems);
        }
        mockRequest.Setup(r => r.Query).Returns(mockQuery);

        return mockRequest;
    }

    // Helper class to mock query collection
    private class MockQueryCollection : System.Collections.Specialized.NameValueCollection
    {
        public new string? this[string key]
        {
            get => base[key];
            set => base[key] = value;
        }
    }

    [Fact]
    public async Task RunAsync_WithValidRequest_ReturnsJobsList()
    {
        // Arrange
        var jobs = new List<JobDocument>
        {
            new JobDocument
            {
                Id = "job-1",
                JobId = "job-1",
                Status = JobStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new JobDocument
            {
                Id = "job-2",
                JobId = "job-2",
                Status = JobStatus.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        _mockJobRepository
            .Setup(r => r.GetAllJobsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var mockRequest = CreateMockRequest();
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var jobList = JsonSerializer.Deserialize<List<JsonElement>>(responseBody);
        
        Assert.NotNull(jobList);
        Assert.Equal(2, jobList.Count);
    }

    [Fact]
    public async Task RunAsync_WithMaxItemsParameter_PassesToRepository()
    {
        // Arrange
        var maxItems = 50;
        _mockJobRepository
            .Setup(r => r.GetAllJobsAsync(maxItems, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobDocument>());

        var mockRequest = CreateMockRequest(maxItems.ToString());
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        _ = await trigger.RunAsync(mockRequest.Object);

        // Assert
        _mockJobRepository.Verify(
            r => r.GetAllJobsAsync(maxItems, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithInvalidMaxItemsTooLow_ReturnsBadRequest()
    {
        // Arrange
        var mockRequest = CreateMockRequest("0");
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
        
        Assert.Equal("Invalid maxItems parameter", errorResponse.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RunAsync_WithInvalidMaxItemsTooHigh_ReturnsBadRequest()
    {
        // Arrange
        var mockRequest = CreateMockRequest("1001");
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
        
        Assert.Equal("Invalid maxItems parameter", errorResponse.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RunAsync_WithRepositoryException_ReturnsInternalServerError()
    {
        // Arrange
        _mockJobRepository
            .Setup(r => r.GetAllJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var mockRequest = CreateMockRequest();
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
        
        Assert.Equal("Failed to fetch jobs", errorResponse.GetProperty("error").GetString());
        Assert.Equal("An internal error occurred while processing the request.", 
            errorResponse.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunAsync_WithInvalidMaxItemsFormat_UsesDefaultValue()
    {
        // Arrange
        _mockJobRepository
            .Setup(r => r.GetAllJobsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobDocument>());

        var mockRequest = CreateMockRequest("invalid");
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _mockJobRepository.Verify(
            r => r.GetAllJobsAsync(100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReturnsJobsInCorrectFormat()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;
        var jobs = new List<JobDocument>
        {
            new JobDocument
            {
                Id = "test-job",
                JobId = "test-job",
                Status = JobStatus.Completed,
                CreatedAt = createdAt
            }
        };

        _mockJobRepository
            .Setup(r => r.GetAllJobsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var mockRequest = CreateMockRequest();
        var trigger = new GetJobsHttpTrigger(_mockJobRepository.Object, _mockLogger.Object);

        // Act
        var response = await trigger.RunAsync(mockRequest.Object);

        // Assert
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var jobList = JsonSerializer.Deserialize<List<JsonElement>>(responseBody);
        
        Assert.NotNull(jobList);
        Assert.Single(jobList);
        Assert.Equal("test-job", jobList[0].GetProperty("jobId").GetString());
        Assert.Equal(JobStatus.Completed, jobList[0].GetProperty("status").GetString());
        Assert.Equal(createdAt.ToString("o"), jobList[0].GetProperty("createdAt").GetString());
    }
}
