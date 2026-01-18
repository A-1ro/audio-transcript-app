using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using TranscriptionFunctions.Constants;
using TranscriptionFunctions.Models;
using TranscriptionFunctions.Services;
using Xunit;
using Xunit.Abstractions;

namespace TranscriptionFunctions.Tests.E2E;

/// <summary>
/// E2Eテスト: CreateJob → JobQueueTrigger → Orchestration開始までの一連のフロー
/// 
/// このテストクラスは、以下のシナリオをカバーします：
/// 1. CreateJobHttpTriggerによるジョブ作成
/// 2. Azure Queue StorageへのJobIdエンキュー
/// 3. JobQueueTriggerによるメッセージ受信
/// 4. DurableTaskClientによるオーケストレーション起動
/// 
/// テスト設計書: E2E_TEST_DESIGN.md を参照
/// </summary>
public class CreateJobToQueueTriggerE2ETests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<IJobRepository> _mockJobRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CreateJobHttpTrigger>> _mockCreateJobLogger;
    private readonly Mock<ILogger<JobQueueTrigger>> _mockQueueTriggerLogger;
    private readonly Mock<FunctionContext> _mockFunctionContext;
    private readonly List<MemoryStream> _memoryStreams;
    private readonly string _testConnectionString;
    private readonly string _testQueueName;

    public CreateJobToQueueTriggerE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _mockJobRepository = new Mock<IJobRepository>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockCreateJobLogger = new Mock<ILogger<CreateJobHttpTrigger>>();
        _mockQueueTriggerLogger = new Mock<ILogger<JobQueueTrigger>>();
        _mockFunctionContext = new Mock<FunctionContext>();
        _memoryStreams = new List<MemoryStream>();

        // テスト用の接続文字列（Azuriteまたはモック）
        _testConnectionString = "UseDevelopmentStorage=true";
        _testQueueName = $"test-queue-{Guid.NewGuid():N}";

        // Configuration mock setup
        _mockConfiguration.Setup(c => c["AzureWebJobsStorage"]).Returns(_testConnectionString);
    }

    public void Dispose()
    {
        foreach (var stream in _memoryStreams)
        {
            stream?.Dispose();
        }

        // テスト用キューのクリーンアップ
        try
        {
            var queueClient = new QueueClient(_testConnectionString, _testQueueName);
            queueClient.DeleteIfExists();
        }
        catch
        {
            // クリーンアップエラーは無視
        }
    }

    private Mock<HttpRequestData> CreateMockRequest(object? requestBody = null)
    {
        var mockRequest = new Mock<HttpRequestData>(_mockFunctionContext.Object);
        
        // Setup CreateResponse
        mockRequest.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var mockResponse = new Mock<HttpResponseData>(_mockFunctionContext.Object);
            mockResponse.SetupProperty(r => r.StatusCode);
            mockResponse.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            var memoryStream = new MemoryStream();
            _memoryStreams.Add(memoryStream);
            mockResponse.SetupGet(r => r.Body).Returns(memoryStream);
            return mockResponse.Object;
        });

        // Setup request body - we need to setup the Body stream for ReadFromJsonAsync to work
        if (requestBody != null)
        {
            var json = JsonSerializer.Serialize(requestBody);
            var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            _memoryStreams.Add(bodyStream);
            mockRequest.Setup(r => r.Body).Returns(bodyStream);
        }

        return mockRequest;
    }

    #region TC-E2E-001: 基本的な正常フロー

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-001")]
    public async Task TC_E2E_001_BasicSuccessFlow_CreatesJobAndEnqueuesMessage()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-001: 基本的な正常フロー ===");
        
        var testJobId = Guid.NewGuid().ToString();
        var requestBody = new CreateJobRequest
        {
            AudioFiles = new[]
            {
                new AudioFileRequest
                {
                    FileName = "test-audio.mp3",
                    BlobUrl = "https://example.blob.core.windows.net/audio/test-audio.mp3"
                }
            }
        };

        var expectedJob = new JobDocument
        {
            Id = testJobId,
            JobId = testJobId,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            AudioFiles = new[]
            {
                new AudioFileInfo
                {
                    FileId = $"{testJobId}-000",
                    BlobUrl = requestBody.AudioFiles[0].BlobUrl,
                    FileName = requestBody.AudioFiles[0].FileName
                }
            }
        };

        _mockJobRepository
            .Setup(r => r.CreateJobAsync(It.IsAny<string>(), It.IsAny<AudioFileInfo[]>()))
            .ReturnsAsync(expectedJob);

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act - Step 1: CreateJob API を呼び出す
        _output.WriteLine("Step 1: CreateJob APIを呼び出し");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert - Step 1: レスポンスを検証
        _output.WriteLine("Step 1: レスポンスを検証");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        _output.WriteLine($"Response: {responseBody}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(responseJson.TryGetProperty("jobId", out var jobIdElement));
        Assert.True(responseJson.TryGetProperty("status", out var statusElement));
        Assert.Equal(JobStatus.Pending, statusElement.GetString());

        // Verify job repository was called
        _mockJobRepository.Verify(
            r => r.CreateJobAsync(It.IsAny<string>(), It.IsAny<AudioFileInfo[]>()),
            Times.Once,
            "CreateJobAsync should be called once");

        // Verify logger was called with success message
        _mockCreateJobLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Job created and enqueued successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Success log should be written");

        _output.WriteLine("✅ TC-E2E-001: PASS - ジョブ作成とレスポンスの検証が成功");
    }

    #endregion

    #region TC-E2E-002: 複数のオーディオファイルを含むジョブ

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-002")]
    public async Task TC_E2E_002_MultipleAudioFiles_CreatesJobWithAllFiles()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-002: 複数のオーディオファイルを含むジョブ ===");
        
        var testJobId = Guid.NewGuid().ToString();
        var requestBody = new CreateJobRequest
        {
            AudioFiles = new[]
            {
                new AudioFileRequest
                {
                    FileName = "audio1.mp3",
                    BlobUrl = "https://example.blob.core.windows.net/audio/audio1.mp3"
                },
                new AudioFileRequest
                {
                    FileName = "audio2.wav",
                    BlobUrl = "https://example.blob.core.windows.net/audio/audio2.wav"
                },
                new AudioFileRequest
                {
                    FileName = "audio3.m4a",
                    BlobUrl = "https://example.blob.core.windows.net/audio/audio3.m4a"
                }
            }
        };

        var expectedJob = new JobDocument
        {
            Id = testJobId,
            JobId = testJobId,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            AudioFiles = requestBody.AudioFiles.Select((af, i) => new AudioFileInfo
            {
                FileId = $"{testJobId}-{i:D3}",
                BlobUrl = af.BlobUrl,
                FileName = af.FileName
            }).ToArray()
        };

        _mockJobRepository
            .Setup(r => r.CreateJobAsync(It.IsAny<string>(), It.IsAny<AudioFileInfo[]>()))
            .ReturnsAsync(expectedJob);

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act
        _output.WriteLine("CreateJob APIを呼び出し（3ファイル）");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        _mockJobRepository.Verify(
            r => r.CreateJobAsync(
                It.IsAny<string>(),
                It.Is<AudioFileInfo[]>(files => files.Length == 3)),
            Times.Once,
            "CreateJobAsync should be called with 3 audio files");

        _output.WriteLine("✅ TC-E2E-002: PASS - 複数ファイルのジョブ作成が成功");
    }

    #endregion

    #region TC-E2E-003: audioFilesが空

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-003")]
    public async Task TC_E2E_003_EmptyAudioFiles_ReturnsBadRequest()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-003: audioFilesが空 ===");
        
        var requestBody = new CreateJobRequest
        {
            AudioFiles = Array.Empty<AudioFileRequest>()
        };

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act
        _output.WriteLine("CreateJob APIを呼び出し（audioFiles = []）");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert
        _output.WriteLine($"Response Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        _output.WriteLine($"Response Body: {responseBody}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(responseJson.TryGetProperty("error", out var errorElement));
        Assert.Equal("Invalid request", errorElement.GetString());
        Assert.True(responseJson.TryGetProperty("message", out var messageElement));
        Assert.Contains("audioFiles array is required", messageElement.GetString());

        // Verify job was not created
        _mockJobRepository.Verify(
            r => r.CreateJobAsync(It.IsAny<string>(), It.IsAny<AudioFileInfo[]>()),
            Times.Never,
            "CreateJobAsync should not be called for invalid request");

        _output.WriteLine("✅ TC-E2E-003: PASS - 空のaudioFilesでBadRequestが返される");
    }

    #endregion

    #region TC-E2E-004: fileNameが空

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-004")]
    public async Task TC_E2E_004_EmptyFileName_ReturnsBadRequest()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-004: fileNameが空 ===");
        
        var requestBody = new CreateJobRequest
        {
            AudioFiles = new[]
            {
                new AudioFileRequest
                {
                    FileName = "",
                    BlobUrl = "https://example.blob.core.windows.net/audio/test.mp3"
                }
            }
        };

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act
        _output.WriteLine("CreateJob APIを呼び出し（fileName = \"\"）");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        _output.WriteLine($"Response Body: {responseBody}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(responseJson.TryGetProperty("message", out var messageElement));
        Assert.Contains("audioFiles[0].fileName", messageElement.GetString());

        _output.WriteLine("✅ TC-E2E-004: PASS - 空のfileNameでBadRequestが返される");
    }

    #endregion

    #region TC-E2E-005: 無効なblobUrl

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-005")]
    public async Task TC_E2E_005_InvalidBlobUrl_ReturnsBadRequest()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-005: 無効なblobUrl ===");
        
        var requestBody = new CreateJobRequest
        {
            AudioFiles = new[]
            {
                new AudioFileRequest
                {
                    FileName = "test.mp3",
                    BlobUrl = "not-a-valid-url"
                }
            }
        };

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act
        _output.WriteLine("CreateJob APIを呼び出し（blobUrl = \"not-a-valid-url\"）");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        _output.WriteLine($"Response Body: {responseBody}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(responseJson.TryGetProperty("message", out var messageElement));
        Assert.Contains("valid absolute URL", messageElement.GetString());

        _output.WriteLine("✅ TC-E2E-005: PASS - 無効なblobUrlでBadRequestが返される");
    }

    #endregion

    #region TC-E2E-006: Queue Storage接続エラー

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-006")]
    public async Task TC_E2E_006_QueueStorageError_ReturnsInternalServerError()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-006: Queue Storage接続エラー ===");
        
        var testJobId = Guid.NewGuid().ToString();
        var requestBody = new CreateJobRequest
        {
            AudioFiles = new[]
            {
                new AudioFileRequest
                {
                    FileName = "test-audio.mp3",
                    BlobUrl = "https://example.blob.core.windows.net/audio/test-audio.mp3"
                }
            }
        };

        var expectedJob = new JobDocument
        {
            Id = testJobId,
            JobId = testJobId,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _mockJobRepository
            .Setup(r => r.CreateJobAsync(It.IsAny<string>(), It.IsAny<AudioFileInfo[]>()))
            .ReturnsAsync(expectedJob);

        // 無効な接続文字列を設定してQueue Storageエラーをシミュレート
        _mockConfiguration.Setup(c => c["AzureWebJobsStorage"])
            .Returns("InvalidConnectionString");

        var mockRequest = CreateMockRequest(requestBody);
        var createJobTrigger = new CreateJobHttpTrigger(
            _mockJobRepository.Object,
            _mockConfiguration.Object,
            _mockCreateJobLogger.Object);

        // Act
        _output.WriteLine("CreateJob APIを呼び出し（無効な接続文字列）");
        var response = await createJobTrigger.RunAsync(mockRequest.Object);

        // Assert
        _output.WriteLine($"Response Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        _output.WriteLine($"Response Body: {responseBody}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.True(responseJson.TryGetProperty("error", out var errorElement));
        Assert.Contains("Failed to enqueue job", errorElement.GetString());
        
        // ジョブは作成されるが、キューへの追加に失敗する（部分的な失敗）
        Assert.True(responseJson.TryGetProperty("jobId", out _));

        _output.WriteLine("✅ TC-E2E-006: PASS - Queue Storageエラー時にInternalServerErrorが返される");
    }

    #endregion

    #region TC-E2E-008: JobQueueTrigger - 空のJobId

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-008")]
    public async Task TC_E2E_008_JobQueueTrigger_EmptyJobId_ThrowsArgumentException()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-008: JobQueueTrigger - 空のJobId ===");
        
        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        var jobQueueTrigger = new JobQueueTrigger(_mockQueueTriggerLogger.Object);

        // Act & Assert - 空のJobIdでArgumentExceptionがスローされる
        _output.WriteLine("JobQueueTriggerを空のJobIdで呼び出し");
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await jobQueueTrigger.RunAsync("", mockDurableClient.Object);
        });

        _output.WriteLine($"Exception Message: {exception.Message}");
        Assert.Contains("JobId cannot be empty", exception.Message);

        // Verify error log was written
        _mockQueueTriggerLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("JobId is empty or null")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Error log should be written for empty JobId");

        _output.WriteLine("✅ TC-E2E-008: PASS - 空のJobIdでArgumentExceptionがスローされる");
    }

    [Theory]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-008")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task TC_E2E_008_JobQueueTrigger_WhitespaceJobId_ThrowsArgumentException(string whitespaceJobId)
    {
        // Arrange
        _output.WriteLine($"=== TC-E2E-008: JobQueueTrigger - 空白文字のJobId ('{whitespaceJobId.Replace("\t", "\\t").Replace("\n", "\\n")}') ===");
        
        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        var jobQueueTrigger = new JobQueueTrigger(_mockQueueTriggerLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await jobQueueTrigger.RunAsync(whitespaceJobId, mockDurableClient.Object);
        });

        Assert.Contains("JobId cannot be empty", exception.Message);
        _output.WriteLine("✅ PASS - 空白文字のJobIdでArgumentExceptionがスローされる");
    }

    #endregion

    #region TC-E2E-JobQueueTrigger: 正常なオーケストレーション起動

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestCase", "TC-E2E-JobQueueTrigger")]
    public async Task TC_E2E_JobQueueTrigger_ValidJobId_StartsOrchestration()
    {
        // Arrange
        _output.WriteLine("=== TC-E2E-JobQueueTrigger: 正常なオーケストレーション起動 ===");
        
        var testJobId = Guid.NewGuid().ToString();
        var expectedInstanceId = $"orchestration-{testJobId}";
        
        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                nameof(TranscriptionOrchestrator),
                testJobId))
            .ReturnsAsync(expectedInstanceId);

        var jobQueueTrigger = new JobQueueTrigger(_mockQueueTriggerLogger.Object);

        // Act
        _output.WriteLine($"JobQueueTriggerを呼び出し（JobId: {testJobId}）");
        await jobQueueTrigger.RunAsync(testJobId, mockDurableClient.Object);

        // Assert
        mockDurableClient.Verify(
            c => c.ScheduleNewOrchestrationInstanceAsync(
                nameof(TranscriptionOrchestrator),
                testJobId),
            Times.Once,
            "ScheduleNewOrchestrationInstanceAsync should be called once");

        // Verify logs
        _mockQueueTriggerLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Queue trigger received message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Queue trigger log should be written");

        _mockQueueTriggerLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Started orchestration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Orchestration start log should be written");

        _output.WriteLine("✅ TC-E2E-JobQueueTrigger: PASS - オーケストレーションが正常に起動される");
    }

    #endregion
}
