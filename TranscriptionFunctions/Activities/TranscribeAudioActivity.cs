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

    // デフォルトの信頼度スコア（Speech Serviceが詳細な信頼度を提供しない場合）
    private const double DefaultConfidenceScore = 0.95;
    
    // Azure Speech Serviceの設定をキャッシュ（パフォーマンス向上のため）
    private static Lazy<(string? key, string? region, string language)> _speechServiceConfig =
        CreateSpeechServiceConfig();

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
    /// </summary>
    internal static void ResetSpeechServiceConfigForTesting()
    {
        _speechServiceConfig = CreateSpeechServiceConfig();
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
                    Confidence = DefaultConfidenceScore,
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
                    recognitionLanguage,
                    tempAudioPath,
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
        using var httpClient = _httpClientFactory.CreateClient();
        
        // URLから拡張子を取得、取得できない場合は.wavをデフォルトとする
        var extension = Path.GetExtension(new Uri(blobUrl).LocalPath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".wav";
        }
        
        // 現在はWAVファイルのみサポート（Speech SDKの制限）
        var supportedExtensions = new[] { ".wav" };
        if (!supportedExtensions.Contains(extension.ToLowerInvariant()))
        {
            throw new NotSupportedException(
                $"Audio file format '{extension}' is not supported. Only WAV files are currently supported.");
        }
        
        // FileIdをサニタイズしてパストラバーサル攻撃を防ぐ
        var sanitizedFileId = Path.GetFileName(fileId);
        if (string.IsNullOrWhiteSpace(sanitizedFileId))
        {
            sanitizedFileId = "audio";
        }
        
        var tempPath = Path.Combine(Path.GetTempPath(), $"{sanitizedFileId}_{Guid.NewGuid()}{extension}");

        _logger.LogDebug("Downloading audio file from {BlobUrl} to {TempPath}", blobUrl, tempPath);

        try
        {
            using var response = await httpClient.GetAsync(blobUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to download audio file from {blobUrl}. " +
                    $"Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
            }

            try
            {
                await using var fileStream = File.Create(tempPath);
                await response.Content.CopyToAsync(fileStream);

                _logger.LogDebug("Downloaded audio file to {TempPath}", tempPath);
                return tempPath;
            }
            catch
            {
                // ダウンロード失敗時に部分的なファイルを削除
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while downloading audio from {BlobUrl}", blobUrl);
            throw new InvalidOperationException($"Failed to download audio file from {blobUrl}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Azure Speech Service のリアルタイム認識機能を使用して音声を文字起こしする。
    /// 注意: このメソッドは Azure AI Speech の Batch Transcription API は使用しておらず、
    /// RecognizeOnceAsync による短い音声（通常 15 秒未満）向けの単発認識のみを行います。
    /// 長い音声ファイルの場合、最初の部分のみが文字起こしされる点に注意してください。
    /// </summary>
    private async Task<(string transcriptText, double confidence)> TranscribeWithSpeechServiceAsync(
        string speechKey,
        string speechRegion,
        string recognitionLanguage,
        string audioFilePath,
        string jobId,
        string fileId)
    {
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = recognitionLanguage;

        using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var result = await recognizer.RecognizeOnceAsync();

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            return (result.Text, CalculateConfidence(result));
        }
        else if (result.Reason == ResultReason.NoMatch)
        {
            _logger.LogWarning(
                "No speech could be recognized from audio file for JobId: {JobId}, FileId: {FileId}",
                jobId,
                fileId);
            return (string.Empty, 0.0);
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            _logger.LogError(
                "Speech recognition canceled for JobId: {JobId}, FileId: {FileId}: Reason={Reason}, ErrorCode={ErrorCode}, ErrorDetails={ErrorDetails}",
                jobId,
                fileId,
                cancellation.Reason,
                cancellation.ErrorCode,
                cancellation.ErrorDetails);
            throw new InvalidOperationException(
                $"Speech recognition failed for JobId={jobId}, FileId={fileId}: {cancellation.ErrorDetails}");
        }
        else
        {
            throw new InvalidOperationException(
                $"Unexpected recognition result for JobId={jobId}, FileId={fileId}: {result.Reason}");
        }
    }

    /// <summary>
    /// 認識結果から簡易的な信頼度スコアを計算する
    /// </summary>
    private double CalculateConfidence(SpeechRecognitionResult result)
    {
        // 現在の実装では、音声が認識されているかどうかのみを判定し、
        // 認識テキストが存在する場合は固定のDefaultConfidenceScoreを返し、
        // 認識テキストが空もしくは空白のみの場合は0.0を返します。
        // 
        // Azure Speech Serviceはresult.Propertiesから単語ごとの詳細な信頼度情報を提供しますが、
        // 本関数では簡易的なスコア算出のみを意図しており、それらの詳細情報は使用していません。
        return !string.IsNullOrWhiteSpace(result.Text) ? DefaultConfidenceScore : 0.0;
    }
}
