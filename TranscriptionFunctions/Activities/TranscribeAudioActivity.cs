using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 音声文字起こしActivity
/// </summary>
public class TranscribeAudioActivity
{
    private readonly ILogger<TranscribeAudioActivity> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

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

        _logger.LogInformation(
            "Transcribing audio for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        try
        {
            // Azure Speech Serviceの設定を環境変数から取得
            var speechKey = Environment.GetEnvironmentVariable("AzureSpeechServiceKey");
            var speechRegion = Environment.GetEnvironmentVariable("AzureSpeechServiceRegion");

            if (string.IsNullOrWhiteSpace(speechKey) || string.IsNullOrWhiteSpace(speechRegion))
            {
                _logger.LogWarning(
                    "Azure Speech Service credentials not configured. Using mock transcription for JobId: {JobId}, FileId: {FileId}",
                    input.JobId,
                    input.FileId);
                
                // 設定が無い場合はモックデータを返す（開発環境用）
                await Task.Delay(500);
                return new TranscriptionResult
                {
                    FileId = input.FileId,
                    TranscriptText = $"Mock transcription for {input.FileId}",
                    Confidence = 0.95,
                    Status = TranscriptionStatus.Completed
                };
            }

            // 音声ファイルをダウンロード
            string tempAudioPath = await DownloadAudioFileAsync(input.BlobUrl, input.FileId);

            try
            {
                // Azure Speech Serviceで文字起こし
                var (transcriptText, confidence) = await TranscribeWithSpeechServiceAsync(
                    speechKey,
                    speechRegion,
                    tempAudioPath);

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
            finally
            {
                // 一時ファイルを削除
                if (File.Exists(tempAudioPath))
                {
                    File.Delete(tempAudioPath);
                    _logger.LogDebug("Deleted temporary audio file: {TempPath}", tempAudioPath);
                }
            }
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
    /// Blobストレージからオーディオファイルをダウンロードする
    /// </summary>
    private async Task<string> DownloadAudioFileAsync(string blobUrl, string fileId)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var tempPath = Path.Combine(Path.GetTempPath(), $"{fileId}_{Guid.NewGuid()}.audio");

        _logger.LogDebug("Downloading audio file from {BlobUrl} to {TempPath}", blobUrl, tempPath);

        var response = await httpClient.GetAsync(blobUrl);
        response.EnsureSuccessStatusCode();

        await using var fileStream = File.Create(tempPath);
        await response.Content.CopyToAsync(fileStream);

        _logger.LogDebug("Downloaded audio file to {TempPath}", tempPath);
        return tempPath;
    }

    /// <summary>
    /// Azure Speech Serviceを使用して音声を文字起こしする
    /// </summary>
    private async Task<(string transcriptText, double confidence)> TranscribeWithSpeechServiceAsync(
        string speechKey,
        string speechRegion,
        string audioFilePath)
    {
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "ja-JP"; // 日本語を設定

        using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var result = await recognizer.RecognizeOnceAsync();

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            return (result.Text, CalculateConfidence(result));
        }
        else if (result.Reason == ResultReason.NoMatch)
        {
            _logger.LogWarning("No speech could be recognized from audio file: {AudioFile}", audioFilePath);
            return (string.Empty, 0.0);
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            _logger.LogError(
                "Speech recognition canceled: Reason={Reason}, ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                cancellation.Reason,
                cancellation.ErrorCode,
                cancellation.ErrorDetails);
            throw new InvalidOperationException($"Speech recognition failed: {cancellation.ErrorDetails}");
        }
        else
        {
            throw new InvalidOperationException($"Unexpected recognition result: {result.Reason}");
        }
    }

    /// <summary>
    /// 認識結果から信頼度を計算する
    /// </summary>
    private double CalculateConfidence(SpeechRecognitionResult result)
    {
        // Azure Speech Serviceは単語ごとの信頼度を提供しますが、
        // ここでは簡易的に全体の品質スコアとして0.95を返します
        // 実際の実装では、result.Propertiesから詳細な信頼度情報を取得可能
        return !string.IsNullOrWhiteSpace(result.Text) ? 0.95 : 0.0;
    }
}
