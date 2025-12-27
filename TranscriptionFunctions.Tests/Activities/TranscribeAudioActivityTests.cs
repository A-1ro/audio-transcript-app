using Microsoft.Extensions.Logging;
using Moq;
using TranscriptionFunctions.Activities;
using TranscriptionFunctions.Models;
using Xunit;

namespace TranscriptionFunctions.Tests.Activities;

/// <summary>
/// TranscribeAudioActivityのテスト
/// </summary>
public class TranscribeAudioActivityTests
{
    private readonly Mock<ILogger<TranscribeAudioActivity>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly TranscribeAudioActivity _activity;

    public TranscribeAudioActivityTests()
    {
        _mockLogger = new Mock<ILogger<TranscribeAudioActivity>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _activity = new TranscribeAudioActivity(_mockLogger.Object, _mockHttpClientFactory.Object);
    }

    [Fact]
    public async Task RunAsync_WithValidInput_ReturnsCompletedResult()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-001.wav"
        };

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.FileId, result.FileId);
        Assert.Equal(TranscriptionStatus.Completed, result.Status);
        Assert.NotEmpty(result.TranscriptText);
        Assert.True(result.Confidence > 0);
    }

    [Theory]
    [InlineData("job-1", "file-1", "https://example.com/audio1.wav")]
    [InlineData("job-2", "file-2", "https://example.com/audio2.wav")]
    public async Task RunAsync_WithDifferentInputs_ReturnsMatchingFileId(
        string jobId,
        string fileId,
        string blobUrl)
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = jobId,
            FileId = fileId,
            BlobUrl = blobUrl
        };

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.Equal(fileId, result.FileId);
    }

    [Fact]
    public async Task RunAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _activity.RunAsync(null!));
    }

    [Theory]
    [InlineData("", "file-1", "https://example.com/audio1.wav")]
    [InlineData("job-1", "", "https://example.com/audio1.wav")]
    [InlineData("job-1", "file-1", "")]
    public async Task RunAsync_WithInvalidInput_ThrowsArgumentException(
        string jobId,
        string fileId,
        string blobUrl)
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = jobId,
            FileId = fileId,
            BlobUrl = blobUrl
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(input));
    }

    [Fact]
    public async Task RunAsync_WithoutSpeechServiceConfiguration_ReturnsMockTranscription()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-001.wav"
        };

        // 環境変数が設定されていないことを確認（デフォルトの状態）
        Environment.SetEnvironmentVariable("AzureSpeechServiceKey", null);
        Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", null);

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.FileId, result.FileId);
        Assert.Equal(TranscriptionStatus.Completed, result.Status);
        Assert.Contains("Mock transcription", result.TranscriptText);
        Assert.True(result.Confidence > 0);
    }

    [Fact]
    public async Task RunAsync_WithMockTranscription_LogsWarningAboutMissingConfiguration()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-456",
            FileId = "file-002",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-002.wav"
        };

        // 環境変数をクリア
        Environment.SetEnvironmentVariable("AzureSpeechServiceKey", null);
        Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", null);

        // Act
        var result = await _activity.RunAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TranscriptionStatus.Completed, result.Status);
        
        // ログに警告が記録されていることを検証
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Azure Speech Service credentials not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }
}
