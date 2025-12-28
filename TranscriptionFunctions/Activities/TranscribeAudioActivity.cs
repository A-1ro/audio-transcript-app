using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 音声文字起こしActivity
/// Azure Speech Service Batch Transcription APIを使用
/// </summary>
public class TranscribeAudioActivity
{
    private readonly ILogger<TranscribeAudioActivity> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // フォールバック用の信頼度スコア（Speech Service Batch APIが詳細な信頼度を提供しない場合にのみ使用）
    private const double FallbackConfidenceScore = 0.95;
    
    // Azure Speech Serviceの設定をキャッシュ（パフォーマンス向上のため）
    private static Lazy<(string? key, string? region, string language)> _speechServiceConfig =
        CreateSpeechServiceConfig();
    
    // ResetSpeechServiceConfigForTestingメソッドのスレッドセーフ性を保証するためのロック
    private static readonly object _configLock = new object();

    /// <summary>
    /// Azure Speech Service 設定用の Lazy インスタンスを作成します。
    /// </summary>
    private static Lazy<(string? key, string? region, string language)> CreateSpeechServiceConfig() =>
        new(() => (
            Environment.GetEnvironmentVariable("AzureSpeechServiceKey"),
            Environment.GetEnvironmentVariable("AzureSpeechServiceRegion"),
            Environment.GetEnvironmentVariable("AzureSpeechServiceLanguage") ?? "ja-JP"
        ));

    /// <summary>
    /// テスト用: キャッシュされた Azure Speech Service 設定をリセットします。
    /// 環境変数を変更した後に呼び出すことで、最新の値を読み込みます。
    /// スレッドセーフな実装のためロックを使用します。
    /// </summary>
    internal static void ResetSpeechServiceConfigForTesting()
    {
        lock (_configLock)
        {
            _speechServiceConfig = CreateSpeechServiceConfig();
        }
    }

    public TranscribeAudioActivity(
        ILogger<TranscribeAudioActivity> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 音声ファイルを文字起こしする
    /// </summary>
    /// <param name="input">文字起こし入力</param>
    /// <returns>文字起こし結果</returns>
    [Function(nameof(TranscribeAudioActivity))]
    public async Task<TranscriptionResult> RunAsync([ActivityTrigger] TranscriptionInput input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.JobId))
        {
            throw new ArgumentException("JobId cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.FileId))
        {
            throw new ArgumentException("FileId cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.BlobUrl))
        {
            throw new ArgumentException("BlobUrl cannot be null or empty", nameof(input));
        }

        // BlobUrlが有効なURIかを検証
        if (!Uri.TryCreate(input.BlobUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"BlobUrl is not a valid URI: {input.BlobUrl}", nameof(input));
        }

        _logger.LogInformation(
            "Transcribing audio for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        try
        {
            // Azure Speech Serviceの設定をキャッシュから取得
            var (speechKey, speechRegion, recognitionLanguage) = _speechServiceConfig.Value;

            if (string.IsNullOrWhiteSpace(speechKey) || string.IsNullOrWhiteSpace(speechRegion))
            {
                var errorMessage = "Azure Speech Service credentials not configured. " +
                    $"Set AzureSpeechServiceKey and AzureSpeechServiceRegion environment variables for JobId: {input.JobId}, FileId: {input.FileId}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Azure Speech Service Batch Transcription APIで文字起こし
            var (transcriptText, confidence) = await TranscribeWithBatchApiAsync(
                speechKey,
                speechRegion,
                recognitionLanguage,
                input.BlobUrl,
                input.JobId,
                input.FileId);

            _logger.LogInformation(
                "Transcription completed for JobId: {JobId}, FileId: {FileId}, Confidence: {Confidence}",
                input.JobId,
                input.FileId,
                confidence);

            return new TranscriptionResult
            {
                FileId = input.FileId,
                TranscriptText = transcriptText,
                Confidence = confidence,
                Status = TranscriptionStatus.Completed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Transcription failed for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);

            // 失敗時の結果を返す
            return new TranscriptionResult
            {
                FileId = input.FileId,
                TranscriptText = string.Empty,
                Confidence = 0.0,
                Status = TranscriptionStatus.Failed
            };
        }
    }

    /// <summary>
    /// Azure Speech Service Batch Transcription APIを使用して音声を文字起こしする
    /// </summary>
    private async Task<(string transcriptText, double confidence)> TranscribeWithBatchApiAsync(
        string speechKey,
        string speechRegion,
        string recognitionLanguage,
        string blobUrl,
        string jobId,
        string fileId)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);

        var baseUrl = $"https://{speechRegion}.api.cognitive.microsoft.com/speechtotext/v3.1";

        try
        {
            // 1. バッチ文字起こしジョブを作成
            var transcriptionId = await CreateBatchTranscriptionAsync(
                httpClient, baseUrl, blobUrl, recognitionLanguage, jobId, fileId);

            _logger.LogInformation(
                "Created batch transcription {TranscriptionId} for JobId: {JobId}, FileId: {FileId}",
                transcriptionId, jobId, fileId);

            // 2. 文字起こし完了を待機
            var transcriptionStatus = await WaitForTranscriptionCompletionAsync(
                httpClient, baseUrl, transcriptionId, jobId, fileId);

            if (transcriptionStatus != "Succeeded")
            {
                throw new InvalidOperationException(
                    $"Batch transcription failed with status: {transcriptionStatus} for JobId={jobId}, FileId={fileId}");
            }

            // 3. 結果を取得
            var (text, confidence) = await GetTranscriptionResultAsync(
                httpClient, baseUrl, transcriptionId, jobId, fileId);

            // 4. クリーンアップ（バッチジョブを削除）
            await DeleteBatchTranscriptionAsync(httpClient, baseUrl, transcriptionId);

            return (text, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Batch transcription failed for JobId: {JobId}, FileId: {FileId}",
                jobId, fileId);
            throw;
        }
    }

    /// <summary>
    /// バッチ文字起こしジョブを作成
    /// </summary>
    private async Task<string> CreateBatchTranscriptionAsync(
        HttpClient httpClient,
        string baseUrl,
        string blobUrl,
        string language,
        string jobId,
        string fileId)
    {
        var requestBody = new
        {
            contentUrls = new[] { blobUrl },
            locale = language,
            displayName = $"Transcription-{jobId}-{fileId}",
            properties = new
            {
                wordLevelTimestampsEnabled = false,
                punctuationMode = "DictatedAndAutomatic",
                profanityFilterMode = "Masked"
            }
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/transcriptions", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<BatchTranscriptionResponse>(responseContent);

        return result?.Self?.Split('/').Last() ?? throw new InvalidOperationException("Failed to get transcription ID");
    }

    /// <summary>
    /// 文字起こし完了を待機
    /// </summary>
    private async Task<string> WaitForTranscriptionCompletionAsync(
        HttpClient httpClient,
        string baseUrl,
        string transcriptionId,
        string jobId,
        string fileId)
    {
        var maxAttempts = 60; // 最大5分待機（5秒間隔）
        var delaySeconds = 5;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await httpClient.GetAsync($"{baseUrl}/transcriptions/{transcriptionId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var status = JsonSerializer.Deserialize<BatchTranscriptionStatusResponse>(content);

            _logger.LogDebug(
                "Batch transcription {TranscriptionId} status: {Status} (attempt {Attempt}/{MaxAttempts}) for JobId: {JobId}, FileId: {FileId}",
                transcriptionId, status?.Status, attempt + 1, maxAttempts, jobId, fileId);

            if (status?.Status == "Succeeded" || status?.Status == "Failed")
            {
                return status.Status;
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        throw new TimeoutException(
            $"Batch transcription {transcriptionId} did not complete within timeout for JobId={jobId}, FileId={fileId}");
    }

    /// <summary>
    /// 文字起こし結果を取得
    /// </summary>
    private async Task<(string text, double confidence)> GetTranscriptionResultAsync(
        HttpClient httpClient,
        string baseUrl,
        string transcriptionId,
        string jobId,
        string fileId)
    {
        // ファイル一覧を取得
        var filesResponse = await httpClient.GetAsync($"{baseUrl}/transcriptions/{transcriptionId}/files");
        filesResponse.EnsureSuccessStatusCode();

        var filesContent = await filesResponse.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<BatchTranscriptionFilesResponse>(filesContent);

        var contentFile = files?.Values?.FirstOrDefault(f => f.Kind == "Transcription");
        if (contentFile == null || contentFile.Links?.ContentUrl == null)
        {
            throw new InvalidOperationException(
                $"No transcription content file found for JobId={jobId}, FileId={fileId}");
        }

        // 結果ファイルをダウンロード
        var resultResponse = await httpClient.GetAsync(contentFile.Links.ContentUrl);
        resultResponse.EnsureSuccessStatusCode();

        var resultContent = await resultResponse.Content.ReadAsStringAsync();
        var transcriptionResult = JsonSerializer.Deserialize<BatchTranscriptionContent>(resultContent);

        // すべての認識結果を結合
        var combinedResults = transcriptionResult?.CombinedRecognizedPhrases?.FirstOrDefault();
        if (combinedResults == null)
        {
            _logger.LogWarning(
                "No recognized phrases found for JobId: {JobId}, FileId: {FileId}",
                jobId, fileId);
            return (string.Empty, 0.0);
        }

        return (combinedResults.Display ?? string.Empty, FallbackConfidenceScore);
    }

    /// <summary>
    /// バッチ文字起こしジョブを削除
    /// </summary>
    private async Task DeleteBatchTranscriptionAsync(
        HttpClient httpClient,
        string baseUrl,
        string transcriptionId)
    {
        try
        {
            await httpClient.DeleteAsync($"{baseUrl}/transcriptions/{transcriptionId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete batch transcription {TranscriptionId}, but continuing",
                transcriptionId);
        }
    }

    // Batch Transcription API レスポンスモデル
    private class BatchTranscriptionResponse
    {
        [JsonPropertyName("self")]
        public string? Self { get; set; }
    }

    private class BatchTranscriptionStatusResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private class BatchTranscriptionFilesResponse
    {
        [JsonPropertyName("values")]
        public List<BatchTranscriptionFile>? Values { get; set; }
    }

    private class BatchTranscriptionFile
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("links")]
        public BatchTranscriptionFileLinks? Links { get; set; }
    }

    private class BatchTranscriptionFileLinks
    {
        [JsonPropertyName("contentUrl")]
        public string? ContentUrl { get; set; }
    }

    private class BatchTranscriptionContent
    {
        [JsonPropertyName("combinedRecognizedPhrases")]
        public List<CombinedRecognizedPhrase>? CombinedRecognizedPhrases { get; set; }
    }

    private class CombinedRecognizedPhrase
    {
        [JsonPropertyName("display")]
        public string? Display { get; set; }
    }
}
