using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Models;

namespace TranscriptionFunctions.Activities;

/// <summary>
/// 文字起こし結果保存Activity
/// </summary>
public class SaveResultActivity
{
    private readonly ILogger<SaveResultActivity> _logger;
    // TODO: 実際の実装ではCosmosDBクライアントとBlobクライアントをDIで注入
    // private readonly CosmosClient _cosmosClient;
    // private readonly BlobServiceClient _blobServiceClient;

    // シミュレーション用の遅延時間
    private const int CosmosDbSimulationDelayMs = 50;
    private const int BlobStorageSimulationDelayMs = 30;

    public SaveResultActivity(ILogger<SaveResultActivity> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 冪等性を保証するためのドキュメントIDを生成する
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    /// <param name="fileId">ファイルID</param>
    /// <returns>ドキュメントID</returns>
    private static string CreateDocumentId(string jobId, string fileId)
    {
        return $"{jobId}_{fileId}";
    }

    /// <summary>
    /// 文字起こし結果を永続化する
    /// </summary>
    /// <param name="input">保存する結果情報</param>
    [Function(nameof(SaveResultActivity))]
    public async Task RunAsync([ActivityTrigger] SaveResultInput input)
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

        if (string.IsNullOrWhiteSpace(input.Status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(input));
        }

        _logger.LogInformation(
            "Saving transcription result for JobId: {JobId}, FileId: {FileId}, Status: {Status}",
            input.JobId,
            input.FileId,
            input.Status);

        // TODO: 実際の実装ではCosmosDBに保存
        // 1. Transcriptions コンテナに対して Upsert 操作を実行
        //    - id: "{JobId}_{FileId}" で一意性を保証（冪等性）
        //    - partitionKey: JobId
        // 2. 既存のドキュメントがある場合は上書き（二重登録を防止）
        
        // 冪等性チェックのシミュレーション
        var documentId = CreateDocumentId(input.JobId, input.FileId);
        _logger.LogInformation(
            "Checking for existing document with id: {DocumentId}",
            documentId);

        // TODO: Cosmos DB での実際のチェック
        // var existingDocument = await container.ReadItemAsync<TranscriptionDocument>(
        //     documentId, 
        //     new PartitionKey(input.JobId));
        
        await Task.Delay(CosmosDbSimulationDelayMs); // データベース操作のシミュレーション

        // TODO: Upsert 操作
        // var document = new TranscriptionDocument
        // {
        //     Id = documentId,
        //     JobId = input.JobId,
        //     FileId = input.FileId,
        //     TranscriptText = input.TranscriptText,
        //     Confidence = input.Confidence,
        //     Status = input.Status,
        //     CreatedAt = DateTime.UtcNow
        // };
        // await container.UpsertItemAsync(document, new PartitionKey(input.JobId));

        _logger.LogInformation(
            "Transcription result saved to Cosmos DB for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);

        // オプション: Raw結果をBlobに保存
        if (!string.IsNullOrWhiteSpace(input.RawResult))
        {
            _logger.LogInformation(
                "Saving raw result to Blob Storage for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);

            // TODO: Blob Storageに保存
            // var blobName = $"{input.JobId}/{input.FileId}_raw.json";
            // var containerClient = _blobServiceClient.GetBlobContainerClient("transcription-results");
            // var blobClient = containerClient.GetBlobClient(blobName);
            // await blobClient.UploadAsync(
            //     BinaryData.FromString(input.RawResult),
            //     overwrite: true);

            await Task.Delay(BlobStorageSimulationDelayMs); // Blob保存のシミュレーション

            _logger.LogInformation(
                "Raw result saved to Blob Storage for JobId: {JobId}, FileId: {FileId}",
                input.JobId,
                input.FileId);
        }

        _logger.LogInformation(
            "Save operation completed for JobId: {JobId}, FileId: {FileId}",
            input.JobId,
            input.FileId);
    }
}
