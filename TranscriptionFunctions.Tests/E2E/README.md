# E2E Tests for CreateJob → JobQueueTrigger Flow

このディレクトリには、CreateJobからJobQueueTriggerを経由してTranscriptionOrchestratorが起動されるまでの一連のフローをテストするE2Eテストが含まれています。

## テストファイル

- **CreateJobToQueueTriggerE2ETests.cs**: E2Eテストの実装

## テストの実行方法

### すべてのE2Eテストを実行

```bash
cd /path/to/audio-transcript-app
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj --filter "Category=E2E"
```

### 特定のテストケースのみ実行

```bash
# TC-E2E-001のみ実行
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "FullyQualifiedName~TC_E2E_001"

# TC-E2E-008（空のJobId）のみ実行
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "FullyQualifiedName~TC_E2E_008"
```

### 詳細な出力を表示

```bash
dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj \
  --filter "Category=E2E" \
  --logger "console;verbosity=detailed"
```

## テストケース一覧

### 正常系

| テストID | メソッド名 | 概要 |
|---------|-----------|------|
| TC-E2E-001 | `TC_E2E_001_BasicSuccessFlow_CreatesJobAndEnqueuesMessage` | 単一のオーディオファイルでジョブ作成 |
| TC-E2E-002 | `TC_E2E_002_MultipleAudioFiles_CreatesJobWithAllFiles` | 複数のオーディオファイルでジョブ作成 |
| TC-E2E-JobQueueTrigger | `TC_E2E_JobQueueTrigger_ValidJobId_StartsOrchestration` | オーケストレーションの起動 |

### 異常系

| テストID | メソッド名 | 概要 |
|---------|-----------|------|
| TC-E2E-003 | `TC_E2E_003_EmptyAudioFiles_ReturnsBadRequest` | 空のaudioFiles配列 |
| TC-E2E-004 | `TC_E2E_004_EmptyFileName_ReturnsBadRequest` | 空のfileName |
| TC-E2E-005 | `TC_E2E_005_InvalidBlobUrl_ReturnsBadRequest` | 無効なblobUrl |
| TC-E2E-006 | `TC_E2E_006_QueueStorageError_ReturnsInternalServerError` | Queue Storageエラー |
| TC-E2E-008 | `TC_E2E_008_JobQueueTrigger_EmptyJobId_ThrowsArgumentException` | 空のJobId |
| TC-E2E-008 | `TC_E2E_008_JobQueueTrigger_WhitespaceJobId_ThrowsArgumentException` | 空白文字のJobId（Theory） |

## テストアーキテクチャ

### モックの使用

このテストスイートは、以下のコンポーネントをモック化しています：

- **IJobRepository**: ジョブデータの永続化
- **IConfiguration**: アプリケーション設定
- **HttpRequestData / HttpResponseData**: HTTPリクエスト/レスポンス
- **DurableTaskClient**: オーケストレーションクライアント
- **ILogger**: ログ出力

### テストの構造

```
CreateJobToQueueTriggerE2ETests
├── Setup (Constructor)
│   ├── Mock<IJobRepository>
│   ├── Mock<IConfiguration>
│   ├── Mock<ILogger<CreateJobHttpTrigger>>
│   ├── Mock<ILogger<JobQueueTrigger>>
│   └── Mock<FunctionContext>
│
├── Helper Methods
│   └── CreateMockRequest() - HTTPリクエストのモック作成
│
├── Test Cases
│   ├── TC_E2E_001_BasicSuccessFlow
│   ├── TC_E2E_002_MultipleAudioFiles
│   ├── TC_E2E_003_EmptyAudioFiles
│   ├── TC_E2E_004_EmptyFileName
│   ├── TC_E2E_005_InvalidBlobUrl
│   ├── TC_E2E_006_QueueStorageError
│   ├── TC_E2E_008_EmptyJobId
│   ├── TC_E2E_008_WhitespaceJobId
│   └── TC_E2E_JobQueueTrigger_ValidJobId
│
└── Cleanup (Dispose)
    └── MemoryStreamのクリーンアップ
```

## 検証項目

各テストケースは以下を検証します：

1. **HTTPステータスコード**
   - 正常: 201 Created
   - 異常: 400 Bad Request / 500 Internal Server Error

2. **レスポンスボディ**
   - jobId、statusフィールドの存在
   - エラーメッセージの内容

3. **メソッド呼び出しの検証**
   - `CreateJobAsync()` の呼び出し回数
   - `ScheduleNewOrchestrationInstanceAsync()` の呼び出し
   - 正しいパラメータでの呼び出し

4. **ログ出力の検証**
   - 成功ログ: "Job created and enqueued successfully"
   - 開始ログ: "Queue trigger received message"
   - オーケストレーションログ: "Started orchestration"

## 関連ドキュメント

- **E2E_TEST_DESIGN.md**: テスト設計書
- **E2E_TEST_PROCEDURE.md**: テスト実施手順書
- **E2E_TEST_RESULTS.md**: テスト結果報告書
- **INTEGRATION_TESTING.md**: 統合テストガイド（親ディレクトリ）

## トラブルシューティング

### テストが失敗する場合

1. **依存パッケージの確認**
   ```bash
   dotnet restore TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj
   ```

2. **ビルドエラーの確認**
   ```bash
   dotnet build TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj
   ```

3. **既存テストの実行**
   ```bash
   # すべてのテストを実行して基本的な問題がないか確認
   dotnet test TranscriptionFunctions.Tests/TranscriptionFunctions.Tests.csproj
   ```

### よくある問題

**Q: テストが見つからない**
A: フィルタ条件を確認してください。`--filter "Category=E2E"` または `--filter "FullyQualifiedName~E2E"` を使用してください。

**Q: モック関連のエラーが出る**
A: Moqパッケージのバージョンを確認してください。このプロジェクトはMoq 4.20.72を使用しています。

**Q: .NET バージョンエラー**
A: テストプロジェクトは.NET 10.0、実装プロジェクトは.NET 8.0を使用しています。両方のSDKがインストールされていることを確認してください。

## 今後の拡張

このテストスイートは、以下のように拡張可能です：

1. **Azuriteを使用した統合テスト**
   - 実際のQueue Storageへのメッセージエンキュー
   - 実際のTable Storageへのデータ保存

2. **パフォーマンステスト**
   - 大量のジョブ作成リクエスト
   - 並行処理のテスト

3. **完全なエンドツーエンドテスト**
   - Azure Functions全体の起動
   - 実際のHTTPリクエストの送信
   - オーケストレーション完了までの追跡

## コントリビューション

テストケースを追加する際は：

1. テストメソッド名を `TC_E2E_XXX_` のプレフィックスで開始
2. `[Trait("Category", "E2E")]` 属性を追加
3. `[Trait("TestCase", "TC-E2E-XXX")]` 属性でテストIDを指定
4. `_output.WriteLine()` で詳細な出力を提供
5. 期待される動作を明確にコメント

## ライセンス

このテストコードは、親プロジェクトと同じライセンスに従います。
