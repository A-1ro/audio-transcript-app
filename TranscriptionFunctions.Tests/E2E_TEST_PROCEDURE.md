# E2Eテスト実施手順書：CreateJob → JobQueueTrigger フロー

## 1. 概要

このドキュメントは、CreateJobからJobQueueTriggerを経由してTranscriptionOrchestratorが起動されるまでのE2Eテストを実施するための詳細な手順書です。

## 2. 前提条件

### 2.1 必要なツール・環境

以下のツールがインストールされていることを確認してください：

```bash
# .NET SDK 8.0以降
dotnet --version
# 期待: 8.0.x or 10.0.x

# Azure Functions Core Tools 4.x
func --version
# 期待: 4.x.x

# Azurite (Azure Storage Emulator)
azurite --version
# 期待: 3.x.x
```

### 2.2 インストールコマンド（必要に応じて）

```bash
# Azure Functions Core Tools
npm install -g azure-functions-core-tools@4

# Azurite
npm install -g azurite
```

## 3. テスト環境セットアップ

### 3.1 Azuriteの起動

新しいターミナルウィンドウを開いて以下を実行：

```bash
# ワークスペースのルートディレクトリで実行
cd /home/runner/work/audio-transcript-app/audio-transcript-app

# Azuriteを起動（バックグラウンドで実行）
mkdir -p azurite
azurite --silent --location ./azurite --debug ./azurite/debug.log &

# プロセスIDを記録
echo $! > azurite.pid
```

**確認方法**:
```bash
# Azuriteが起動していることを確認
ps aux | grep azurite
```

### 3.2 ローカル設定ファイルの準備

```bash
cd TranscriptionFunctions

# local.settings.jsonが存在しない場合は作成
if [ ! -f local.settings.json ]; then
  cp local.settings.json.template local.settings.json
fi

# 設定内容を確認・編集
cat local.settings.json
```

**必須の設定項目**:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "TableStorageConnectionString": "UseDevelopmentStorage=true"
  }
}
```

### 3.3 Azure Functionsの起動

TranscriptionFunctionsディレクトリで別のターミナルウィンドウを開いて実行：

```bash
cd /home/runner/work/audio-transcript-app/audio-transcript-app/TranscriptionFunctions

# Functions Appをビルドして起動
func start --port 7071
```

**起動確認**:
以下のような出力が表示されることを確認：

```
Functions:
        CreateJob: [POST] http://localhost:7071/api/jobs
        GetJobsHttpTrigger: [GET] http://localhost:7071/api/jobs
        JobQueueTrigger: QueueTrigger
        TranscriptionOrchestrator: orchestrationTrigger

For detailed output, run func with --verbose flag.
```

## 4. 自動E2Eテストの実行

### 4.1 テストプロジェクトのビルド

```bash
cd /home/runner/work/audio-transcript-app/audio-transcript-app

# テストプロジェクトをビルド
dotnet build TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj
```

### 4.2 E2Eテストの実行

```bash
# すべてのE2Eテストを実行
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "FullyQualifiedName~E2E" \
  --logger "console;verbosity=detailed"

# 特定のテストケースのみ実行する場合
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "FullyQualifiedName~TC_E2E_001" \
  --logger "console;verbosity=detailed"
```

### 4.3 テスト結果の確認

テスト実行後、以下を確認：

1. **テスト結果サマリー**
   ```
   Passed!  - Failed:     0, Passed:     X, Skipped:     0
   ```

2. **詳細ログ**
   - 各テストケースの実行状況
   - エラーメッセージ（失敗した場合）

## 5. 手動テストの実施（オプション）

自動テストに加えて、手動でAPIを呼び出して動作を確認することも可能です。

### 5.1 テストケース TC-E2E-001: 基本的な正常フロー

#### Step 1: CreateJob APIを呼び出す

```bash
curl -X POST http://localhost:7071/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "audioFiles": [
      {
        "fileName": "test-audio.mp3",
        "blobUrl": "https://example.blob.core.windows.net/audio/test-audio.mp3"
      }
    ]
  }'
```

**期待されるレスポンス**:
```json
{
  "jobId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Pending"
}
```

**エビデンス**: レスポンスをコピーして保存

#### Step 2: Functions Appのログを確認

Functions Appを起動しているターミナルで以下のログを確認：

```
[2024-xx-xx xx:xx:xx] Processing POST /api/jobs request
[2024-xx-xx xx:xx:xx] Job created and enqueued successfully. JobId: {JobId}, AudioFiles: 1
[2024-xx-xx xx:xx:xx] Queue trigger received message: {JobId}
[2024-xx-xx xx:xx:xx] Started orchestration with ID = '{InstanceId}' for JobId = '{JobId}'
```

**エビデンス**: ログ出力をコピーして保存、またはスクリーンショットを撮る

#### Step 3: Queue Storageを確認（オプション）

Azure Storage Explorerまたは以下のC#スクリプトで確認：

```bash
# dotnet-scriptを使用する場合
dotnet script CheckQueue.csx
```

`CheckQueue.csx`の内容:
```csharp
#r "nuget: Azure.Storage.Queues, 12.19.1"

using Azure.Storage.Queues;

var connectionString = "UseDevelopmentStorage=true";
var queueName = "transcription-jobs";

var queueClient = new QueueClient(connectionString, queueName);
var properties = await queueClient.GetPropertiesAsync();

Console.WriteLine($"Queue: {queueName}");
Console.WriteLine($"Approximate message count: {properties.Value.ApproximateMessagesCount}");
```

#### Step 4: Table Storageを確認（オプション）

```csharp
#r "nuget: Azure.Data.Tables, 12.9.1"

using Azure.Data.Tables;

var connectionString = "UseDevelopmentStorage=true";
var tableName = "Jobs";

var tableClient = new TableClient(connectionString, tableName);

// 最新のジョブを取得
var entities = tableClient.Query<TableEntity>();
foreach (var entity in entities.Take(5))
{
    Console.WriteLine($"JobId: {entity.PartitionKey}");
    Console.WriteLine($"Status: {entity["Status"]}");
    Console.WriteLine($"CreatedAt: {entity["CreatedAt"]}");
    Console.WriteLine("---");
}
```

**エビデンス**: クエリ結果をコピーして保存

---

### 5.2 テストケース TC-E2E-003: audioFilesが空

```bash
curl -X POST http://localhost:7071/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "audioFiles": []
  }'
```

**期待されるレスポンス**:
```json
{
  "error": "Invalid request",
  "message": "audioFiles array is required and must not be empty"
}
```

**確認**: HTTPステータスコードが400であることを確認

```bash
curl -i -X POST http://localhost:7071/api/jobs \
  -H "Content-Type: application/json" \
  -d '{"audioFiles": []}'
```

---

### 5.3 テストケース TC-E2E-004: fileNameが空

```bash
curl -X POST http://localhost:7071/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "audioFiles": [
      {
        "fileName": "",
        "blobUrl": "https://example.blob.core.windows.net/audio/test.mp3"
      }
    ]
  }'
```

**期待されるレスポンス**:
```json
{
  "error": "Invalid request",
  "message": "audioFiles[0].fileName is required and must not be empty"
}
```

---

### 5.4 テストケース TC-E2E-005: 無効なblobUrl

```bash
curl -X POST http://localhost:7071/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "audioFiles": [
      {
        "fileName": "test.mp3",
        "blobUrl": "not-a-valid-url"
      }
    ]
  }'
```

**期待されるレスポンス**:
```json
{
  "error": "Invalid request",
  "message": "audioFiles[0].blobUrl must be a valid absolute URL"
}
```

---

## 6. エビデンス収集

### 6.1 収集すべきエビデンス

各テストケースで以下を収集してください：

1. **APIレスポンス**
   - HTTPステータスコード
   - レスポンスボディ（JSON）
   - 実行日時

2. **Functions Appログ**
   - CreateJobHttpTriggerのログ
   - JobQueueTriggerのログ
   - TranscriptionOrchestratorのログ（開始ログのみ）

3. **Storage確認結果**
   - Queue Storageのメッセージ数
   - Table Storageのジョブレコード

### 6.2 エビデンスファイルの保存

```bash
# エビデンス用ディレクトリを作成
mkdir -p TranscriptionFunctions.Tests/E2E_Evidence

# テスト実行結果を保存
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "FullyQualifiedName~E2E" \
  --logger "trx;LogFileName=e2e-test-results.trx" \
  --logger "html;LogFileName=e2e-test-results.html"

# 結果ファイルを確認
ls -la TranscriptionFunctions.Tests/TestResults/
```

### 6.3 ログファイルの収集

```bash
# Azuriteのログを確認
cat azurite/debug.log

# Functions Appのログは標準出力をリダイレクトして保存
# （起動時に `func start > functions.log 2>&1` として実行した場合）
cat functions.log | grep -E "(CreateJob|JobQueue|orchestration)"
```

## 7. テスト環境のクリーンアップ

テスト完了後、以下の手順で環境をクリーンアップしてください：

### 7.1 Functions Appの停止

Functions Appを起動しているターミナルで `Ctrl+C` を押す

### 7.2 Azuriteの停止

```bash
# プロセスIDファイルから停止
if [ -f azurite.pid ]; then
  kill $(cat azurite.pid)
  rm azurite.pid
fi

# または、プロセスを検索して停止
pkill -f azurite
```

### 7.3 テストデータのクリーンアップ（オプション）

```bash
# Azuriteのデータディレクトリを削除（完全なクリーンアップ）
rm -rf azurite/

# 次回テスト用に空のディレクトリを再作成
mkdir -p azurite
```

## 8. トラブルシューティング

### 8.1 Azuriteが起動しない

**症状**: `azurite` コマンドが見つからない

**解決方法**:
```bash
# Azuriteをグローバルにインストール
npm install -g azurite

# パスを確認
which azurite
```

---

### 8.2 Functions Appが起動しない

**症状**: `func start` で依存関係のエラーが出る

**解決方法**:
```bash
# NuGetパッケージをリストア
dotnet restore TranscriptionFunctions/TranscriptionFunctions.csproj

# ビルドして確認
dotnet build TranscriptionFunctions/TranscriptionFunctions.csproj
```

---

### 8.3 Queue Triggerが発火しない

**症状**: メッセージがエンキューされているのにJobQueueTriggerが実行されない

**確認事項**:
1. `local.settings.json` の `AzureWebJobsStorage` が正しく設定されているか
2. Azuriteが起動しているか
3. Queueの名前が `transcription-jobs` と一致しているか

**解決方法**:
```bash
# Functions Appを再起動
# Ctrl+C で停止して再度 func start
```

---

### 8.4 Table Storageにジョブが保存されない

**症状**: CreateJob APIは成功するがTable Storageにレコードがない

**確認事項**:
1. `local.settings.json` の `TableStorageConnectionString` が設定されているか
2. Azuriteが起動しているか

**解決方法**:
```bash
# Azuriteのログを確認
cat azurite/debug.log | grep ERROR

# 接続文字列を確認
cat TranscriptionFunctions/local.settings.json | grep TableStorage
```

---

## 9. チェックリスト

テスト実施前に以下を確認してください：

- [ ] .NET SDK 8.0以降がインストールされている
- [ ] Azure Functions Core Tools 4.xがインストールされている
- [ ] Azuriteがインストールされている
- [ ] `local.settings.json` が正しく設定されている
- [ ] Azuriteが起動している
- [ ] Functions Appが起動している
- [ ] CreateJob APIが応答する（`curl http://localhost:7071/api/jobs` でテスト）

テスト実施後に以下を確認してください：

- [ ] すべてのテストケースが実行された
- [ ] 正常系テストがすべてPASSした
- [ ] 異常系テストが期待通りのエラーを返した
- [ ] エビデンスが収集された
- [ ] Functions Appを停止した
- [ ] Azuriteを停止した

## 10. 次のステップ

テスト実施後は、以下のドキュメントを作成してください：

1. **E2E_TEST_RESULTS.md**: テスト結果とエビデンスをまとめたドキュメント
2. 問題が見つかった場合は、GitHubのIssueとして報告

---

以上でE2Eテストの実施手順は完了です。
