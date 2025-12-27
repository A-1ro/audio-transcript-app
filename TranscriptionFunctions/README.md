# Transcription Functions

Azure Functions プロジェクト（.NET 8 Isolated）

## 概要

音声ファイルのバッチ文字起こし処理を実行する Azure Functions。

## 機能

### JobQueueTrigger

- **役割**: Queueメッセージを受け取り、TranscriptionOrchestratorを起動
- **トリガー**: Azure Storage Queue (`transcription-jobs`)
- **入力**: JobId（文字列）
- **処理**: 
  - メッセージからJobIdを抽出
  - TranscriptionOrchestratorを起動
  - エラー時にログ出力

### TranscriptionOrchestrator

- **役割**: ジョブ単位の処理全体を制御（Durable Functions Orchestrator）
- **入力**: JobId
- **処理**（予定）:
  - ジョブ情報取得
  - 音声ファイル一覧取得
  - Activity fan-out（並列文字起こし）
  - 結果集約
  - 状態更新

## 必要な依存関係

- .NET 8 SDK
- Azure Functions Core Tools
- Azure Storage Emulator (ローカル開発時)

## セットアップ

### 1. 設定ファイルの作成

```bash
cp local.settings.json.template local.settings.json
```

### 2. ローカル実行

```bash
dotnet build
func start
```

または

```bash
dotnet run
```

## アーキテクチャ

詳細は以下のドキュメントを参照:
- [docs/2.functions-transcription-design.md](../docs/2.functions-transcription-design.md)

## 技術スタック

- **フレームワーク**: Azure Functions V4
- **ランタイム**: .NET 8 (Isolated)
- **オーケストレーション**: Durable Functions
- **キュー**: Azure Storage Queue
- **ログ**: Application Insights
