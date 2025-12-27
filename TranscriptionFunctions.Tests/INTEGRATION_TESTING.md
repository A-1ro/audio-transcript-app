# Integration Testing Guide

## 概要

JobQueueTrigger と TranscriptionOrchestrator の統合テストガイド

## 前提条件

### 必要なツール

1. **Azure Functions Core Tools**
   ```bash
   npm install -g azure-functions-core-tools@4
   ```

2. **Azurite（Azure Storage Emulator）**
   ```bash
   npm install -g azurite
   ```

## ローカルテスト手順

### 1. Azuriteの起動

```bash
azurite --silent --location ./azurite --debug ./azurite/debug.log
```

### 2. Functions Appの起動

```bash
cd TranscriptionFunctions
func start
```

または

```bash
dotnet run
```

### 3. Queueへメッセージ送信

Azure Storage Explorerまたは以下のC#スクリプトを使用:

```csharp
using Azure.Storage.Queues;

var connectionString = "UseDevelopmentStorage=true";
var queueName = "transcription-jobs";

var queueClient = new QueueClient(connectionString, queueName);
await queueClient.CreateIfNotExistsAsync();

// JobIdをメッセージとして送信
await queueClient.SendMessageAsync("test-job-123");
```

### 4. ログの確認

Functions Appのコンソール出力で以下を確認:

```
[2024-xx-xx xx:xx:xx] Queue trigger received message: test-job-123
[2024-xx-xx xx:xx:xx] Started orchestration with ID = 'xxx' for JobId = 'test-job-123'
[2024-xx-xx xx:xx:xx] Starting transcription orchestration for JobId: test-job-123
```

## テストケース

### 正常系

- [x] 有効なJobIdでメッセージを送信
- [x] Orchestratorが正常に起動される
- [x] ログが正しく出力される

### 異常系

- [x] 空のメッセージを送信
- [x] エラーログが出力される
- [x] 例外が適切に処理される

## トラブルシューティング

### Queueが見つからない

```bash
# Queueを手動で作成
az storage queue create --name transcription-jobs --connection-string "UseDevelopmentStorage=true"
```

### Orchestratorが起動しない

- `host.json` の設定を確認
- Durable Functions拡張がインストールされているか確認
- Application Insightsの設定を確認

## Azure環境でのテスト

### 1. リソースの作成

```bash
# Resource Group
az group create --name rg-transcription --location japaneast

# Storage Account
az storage account create --name sttranscription --resource-group rg-transcription --location japaneast

# Function App
az functionapp create --name func-transcription --resource-group rg-transcription --storage-account sttranscription --runtime dotnet-isolated --functions-version 4
```

### 2. デプロイ

```bash
cd TranscriptionFunctions
func azure functionapp publish func-transcription
```

### 3. テスト

```bash
# Queueにメッセージを送信
az storage message put --queue-name transcription-jobs --content "azure-test-job-123" --connection-string "<YOUR_CONNECTION_STRING>"
```

## 参考資料

- [Azure Functions Core Tools](https://docs.microsoft.com/ja-jp/azure/azure-functions/functions-run-local)
- [Azurite](https://docs.microsoft.com/ja-jp/azure/storage/common/storage-use-azurite)
- [Durable Functions](https://docs.microsoft.com/ja-jp/azure/azure-functions/durable/durable-functions-overview)
