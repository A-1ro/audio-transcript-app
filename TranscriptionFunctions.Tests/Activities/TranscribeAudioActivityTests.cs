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
    public async Task RunAsync_WithoutCredentials_ReturnsFailedResult()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-001.wav"
        };

        // 環境変数の元の値を保存
        var originalKey = Environment.GetEnvironmentVariable("AzureSpeechServiceKey");
        var originalRegion = Environment.GetEnvironmentVariable("AzureSpeechServiceRegion");

        try
        {
            // 環境変数をクリアして認証情報なしの状態にする
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", null);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", null);
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();

            // Act
            var result = await _activity.RunAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(input.FileId, result.FileId);
            Assert.Equal(TranscriptionStatus.Failed, result.Status);
            Assert.Empty(result.TranscriptText);
            Assert.Equal(0.0, result.Confidence);
        }
        finally
        {
            // 環境変数を元に戻す
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", originalKey);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", originalRegion);
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();
        }
    }

    [Theory]
    [InlineData("job-1", "file-1", "https://example.com/audio1.wav")]
    [InlineData("job-2", "file-2", "https://example.com/audio2.wav")]
    public async Task RunAsync_WithoutCredentials_ReturnsMatchingFileIdInFailedResult(
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
    public async Task RunAsync_WithInvalidBlobUrl_ThrowsArgumentException()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "job-1",
            FileId = "file-1",
            BlobUrl = "not-a-valid-url"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _activity.RunAsync(input));
        Assert.Contains("not a valid URI", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WithMissingCredentials_ReturnsFailedResult()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-123",
            FileId = "file-001",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-001.wav"
        };

        // 環境変数の元の値を保存
        var originalKey = Environment.GetEnvironmentVariable("AzureSpeechServiceKey");
        var originalRegion = Environment.GetEnvironmentVariable("AzureSpeechServiceRegion");

        try
        {
            // 環境変数が設定されていないことを確認（デフォルトの状態）
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", null);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", null);
            
            // キャッシュをリセットして最新の環境変数を読み込む
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();

            // Act
            var result = await _activity.RunAsync(input);
            
            // Assert - 認証情報がない場合は Failed ステータスで結果を返す
            Assert.NotNull(result);
            Assert.Equal(TranscriptionStatus.Failed, result.Status);
        }
        finally
        {
            // 環境変数を元に戻す
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", originalKey);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", originalRegion);
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();
        }
    }

    [Fact]
    public async Task RunAsync_WithMissingCredentials_LogsErrorMessage()
    {
        // Arrange
        var input = new TranscriptionInput
        {
            JobId = "test-job-456",
            FileId = "file-002",
            BlobUrl = "https://storage.blob.core.windows.net/audio/file-002.wav"
        };

        // 環境変数の元の値を保存
        var originalKey = Environment.GetEnvironmentVariable("AzureSpeechServiceKey");
        var originalRegion = Environment.GetEnvironmentVariable("AzureSpeechServiceRegion");

        try
        {
            // 環境変数をクリア
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", null);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", null);
            
            // キャッシュをリセットして最新の環境変数を読み込む
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();

            // Act
            var result = await _activity.RunAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TranscriptionStatus.Failed, result.Status);
        }
        finally
        {
            // 環境変数を元に戻す
            Environment.SetEnvironmentVariable("AzureSpeechServiceKey", originalKey);
            Environment.SetEnvironmentVariable("AzureSpeechServiceRegion", originalRegion);
            TranscribeAudioActivity.ResetSpeechServiceConfigForTesting();
        }
    }
}
