# E2Eテスト設計書：CreateJob → JobQueueTrigger フロー

## 1. テスト目的

CreateJobHttpTrigger（HTTP API）からJobQueueTrigger（Queueトリガー）を経由してTranscriptionOrchestratorが起動されるまでの一連のフローが正常に動作することをエンドツーエンド（E2E）で検証する。

## 2. テスト対象範囲

### 2.1 対象コンポーネント
- **CreateJobHttpTrigger**: ジョブ作成HTTPエンドポイント
- **Azure Queue Storage**: `transcription-jobs` キュー
- **JobQueueTrigger**: Queueメッセージトリガー
- **DurableTaskClient**: オーケストレーション起動クライアント
- **TranscriptionOrchestrator**: 文字起こしオーケストレーター

### 2.2 検証ポイント
1. CreateJob APIがジョブを作成しDBに保存できること
2. ジョブIDがQueueに正常にエンキューされること
3. JobQueueTriggerがQueueメッセージを受信して起動すること
4. JobQueueTriggerがオーケストレーションを正常に開始できること
5. 各ステップで適切なログが出力されること
6. エラー時に適切なエラーハンドリングが行われること

## 3. テスト環境

### 3.1 想定環境
- **ローカル開発環境**（推奨）
  - Azurite（Azure Storage Emulator）
  - Azure Functions Core Tools
  - .NET 8.0 SDK
  
- **CI環境**（オプション）
  - GitHub Actions
  - Azurite in Docker
  
- **Azure環境**（オプション）
  - 実際のAzure Storage Account
  - 実際のAzure Functions

### 3.2 必要なリソース
- Azure Storage Account（またはAzurite）
- Azure Table Storage（ジョブ管理用）
- Azure Queue Storage（`transcription-jobs` キュー）
- Application Insights（ログ収集・オプション）

## 4. テストケース設計

### 4.1 正常系テストケース

#### TC-E2E-001: 基本的な正常フロー
**目的**: 単一のオーディオファイルを持つジョブが正常に作成され、オーケストレーションが開始される

**前提条件**:
- Azuriteまたは Azure Storage Accountが起動している
- Table Storageが利用可能
- Queue Storageが利用可能
- Functions Appが起動している

**入力データ**:
```json
{
  "audioFiles": [
    {
      "fileName": "test-audio.mp3",
      "blobUrl": "https://example.blob.core.windows.net/audio/test-audio.mp3"
    }
  ]
}
```

**実行手順**:
1. CreateJob APIに上記のJSONをPOST
2. レスポンスを確認
3. Queue Storageにメッセージがエンキューされたことを確認
4. JobQueueTriggerのログを確認
5. オーケストレーション開始ログを確認

**期待結果**:
- CreateJob APIがHTTP 201を返す
- レスポンスに`jobId`と`status: "Pending"`が含まれる
- `transcription-jobs` キューにjobIdがエンキューされる
- JobQueueTriggerが起動し、"Queue trigger received message"ログが出力される
- "Started orchestration with ID"ログが出力される
- Table Storageにジョブレコードが作成される

**エビデンス収集項目**:
- APIレスポンス（JSON）
- Queue Storageのメッセージ内容
- Functionsのログ出力
- Table Storageのジョブレコード（スクリーンショット or クエリ結果）

---

#### TC-E2E-002: 複数のオーディオファイルを含むジョブ
**目的**: 複数のオーディオファイルを持つジョブが正常に処理される

**前提条件**: TC-E2E-001と同じ

**入力データ**:
```json
{
  "audioFiles": [
    {
      "fileName": "audio1.mp3",
      "blobUrl": "https://example.blob.core.windows.net/audio/audio1.mp3"
    },
    {
      "fileName": "audio2.wav",
      "blobUrl": "https://example.blob.core.windows.net/audio/audio2.wav"
    },
    {
      "fileName": "audio3.m4a",
      "blobUrl": "https://example.blob.core.windows.net/audio/audio3.m4a"
    }
  ]
}
```

**実行手順**: TC-E2E-001と同じ

**期待結果**:
- CreateJob APIがHTTP 201を返す
- レスポンスに`jobId`と`status: "Pending"`が含まれる
- Table Storageのジョブレコードに3つのオーディオファイル情報が含まれる
- JobQueueTriggerが正常に起動する

**エビデンス収集項目**: TC-E2E-001と同じ

---

### 4.2 異常系テストケース

#### TC-E2E-003: 不正なリクエストボディ（audioFiles が空）
**目的**: 入力バリデーションが正しく機能することを確認

**前提条件**: TC-E2E-001と同じ

**入力データ**:
```json
{
  "audioFiles": []
}
```

**期待結果**:
- CreateJob APIがHTTP 400を返す
- エラーメッセージ "audioFiles array is required and must not be empty" が返される
- Queue Storageにメッセージがエンキューされない
- Table Storageにジョブレコードが作成されない

**エビデンス収集項目**:
- APIエラーレスポンス
- Queue Storageが空であることの確認

---

#### TC-E2E-004: 不正なリクエストボディ（fileNameが空）
**目的**: 各オーディオファイルのバリデーションが正しく機能することを確認

**前提条件**: TC-E2E-001と同じ

**入力データ**:
```json
{
  "audioFiles": [
    {
      "fileName": "",
      "blobUrl": "https://example.blob.core.windows.net/audio/test.mp3"
    }
  ]
}
```

**期待結果**:
- CreateJob APIがHTTP 400を返す
- エラーメッセージ "audioFiles[0].fileName is required and must not be empty" が返される
- Queue Storageにメッセージがエンキューされない

**エビデンス収集項目**: TC-E2E-003と同じ

---

#### TC-E2E-005: 不正なblobUrl（無効なURL）
**目的**: URL形式のバリデーションが正しく機能することを確認

**前提条件**: TC-E2E-001と同じ

**入力データ**:
```json
{
  "audioFiles": [
    {
      "fileName": "test.mp3",
      "blobUrl": "not-a-valid-url"
    }
  ]
}
```

**期待結果**:
- CreateJob APIがHTTP 400を返す
- エラーメッセージ "audioFiles[0].blobUrl must be a valid absolute URL" が返される
- Queue Storageにメッセージがエンキューされない

**エビデンス収集項目**: TC-E2E-003と同じ

---

#### TC-E2E-006: Queue Storage接続エラー
**目的**: Queue Storageへの接続失敗時の挙動を確認

**前提条件**:
- Table Storageは利用可能
- Queue Storageが利用不可（Azuriteを停止するなど）

**入力データ**: TC-E2E-001と同じ

**期待結果**:
- CreateJob APIがHTTP 500を返す
- エラーメッセージ "Failed to enqueue job" が返される
- レスポンスに作成された`jobId`が含まれる（部分的な失敗）
- Table Storageにはジョブレコードが作成される（Pending状態のまま）

**エビデンス収集項目**:
- APIエラーレスポンス
- Table Storageのジョブレコード（Pending状態のまま残る）
- エラーログ

---

### 4.3 境界値・エッジケーステストケース

#### TC-E2E-007: 最大数のオーディオファイル（50ファイル）
**目的**: 最大許容数のファイルが正常に処理されることを確認

**前提条件**: TC-E2E-001と同じ

**入力データ**: 50個のaudioFilesを含むJSON

**期待結果**: 正常にジョブが作成され、オーケストレーションが開始される

---

#### TC-E2E-008: 空のJobId（異常ケース）
**目的**: JobQueueTriggerが空のJobIdを適切に拒否することを確認

**前提条件**: TC-E2E-001と同じ

**実行手順**: Queue Storageに空文字列または空白文字列をメッセージとして直接送信

**期待結果**:
- JobQueueTriggerがArgumentExceptionをスローする
- エラーログ "JobId is empty or null" が出力される

**エビデンス収集項目**: エラーログ

---

## 5. ログ確認項目

### 5.1 CreateJobHttpTriggerのログ
- ✅ "Processing POST /api/jobs request"
- ✅ "Job created and enqueued successfully. JobId: {JobId}, AudioFiles: {AudioFileCount}"
- ❌ "Failed to enqueue job message for JobId: {JobId}" (異常系)

### 5.2 JobQueueTriggerのログ
- ✅ "Queue trigger received message: {Message}"
- ✅ "Started orchestration with ID = '{InstanceId}' for JobId = '{JobId}'"
- ❌ "JobId is empty or null" (異常系)
- ❌ "Failed to start orchestration for message: {Message}" (異常系)

### 5.3 TranscriptionOrchestratorのログ（参考）
- ✅ "Starting transcription orchestration for JobId: {JobId}"

## 6. データベース確認項目

### 6.1 Table Storage - Jobsテーブル
```
PartitionKey: {JobId}
RowKey: "Job"
Status: "Pending" → "Processing" → "Completed" or "Failed"
CreatedAt: タイムスタンプ
AudioFiles: オーディオファイル情報のJSON配列
```

**確認クエリ例**:
```csharp
var jobEntity = await tableClient.GetEntityAsync<JobEntity>(jobId, "Job");
```

## 7. テスト実施スケジュール

### Phase 1: ローカル環境でのテスト実施
- テスト環境セットアップ（Azurite）
- 正常系テストケース実施（TC-E2E-001, TC-E2E-002）
- 異常系テストケース実施（TC-E2E-003～006）
- エッジケーステスト実施（TC-E2E-007, 008）

### Phase 2: エビデンス収集
- ログ出力の収集
- スクリーンショットの取得
- データベースクエリ結果の記録

### Phase 3: 結果まとめ
- テスト結果の集計
- 問題点の洗い出し
- 改善提案のまとめ

## 8. 成功基準

以下の条件をすべて満たすこと:

1. ✅ すべての正常系テストケースがPASSする
2. ✅ すべての異常系テストケースが期待通りのエラーハンドリングを行う
3. ✅ 各ステップで適切なログが出力される
4. ✅ データベースの状態が期待通りに更新される
5. ✅ Queue Storageへのメッセージエンキュー・デキューが正常に動作する
6. ✅ オーケストレーションが正常に開始される

## 9. リスクと制約事項

### リスク
- Azuriteとの互換性の問題
- 非同期処理のタイミング依存
- ネットワーク遅延による不安定なテスト結果

### 制約事項
- 実際の音声ファイルのダウンロード・文字起こしは行わない（モックデータを使用）
- オーケストレーション全体の完了までは追跡しない（開始確認まで）
- 本番環境でのテストは実施しない

## 10. 参考資料

- CreateJobHttpTrigger.cs: `/TranscriptionFunctions/CreateJobHttpTrigger.cs`
- JobQueueTrigger.cs: `/TranscriptionFunctions/JobQueueTrigger.cs`
- INTEGRATION_TESTING.md: `/TranscriptionFunctions.Tests/INTEGRATION_TESTING.md`
- Azure Queue Storage ドキュメント: https://docs.microsoft.com/azure/storage/queues/
- Durable Functions ドキュメント: https://docs.microsoft.com/azure/azure-functions/durable/
