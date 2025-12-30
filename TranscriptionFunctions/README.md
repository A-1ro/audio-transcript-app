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
- **処理**:
  - ジョブ情報取得
  - 音声ファイル一覧取得
  - Activity fan-out（並列文字起こし）
  - 結果集約（fan-in）
  - 状態更新

## 並列処理制御

### 並列実行数の制御

音声ファイルの文字起こし処理は、以下の2段階で並列実行数を制御できます：

#### 1. host.json による Activity Function レベルの制御

`host.json` の `extensions.durableTask.maxConcurrentActivityFunctions` で、
ホストインスタンス単位での同時実行可能なActivity関数数を制限します。

```json
{
  "extensions": {
    "durableTask": {
      "maxConcurrentActivityFunctions": 10
    }
  }
}
```

- **デフォルト値**: 10
- **推奨値**: 実行環境のリソース（CPU、メモリ）に応じて調整
- **影響範囲**: Functions ホスト全体

#### 2. 環境変数によるオーケストレーターレベルの制御

環境変数 `Transcription:MaxParallelFiles` で、1つのジョブ内で同時に処理する
音声ファイル数を制限します。

```json
{
  "Values": {
    "Transcription:MaxParallelFiles": "5"
  }
}
```

- **デフォルト値**: 5
- **推奨値**: ジョブあたりの適切な並列数（外部APIのレート制限を考慮）
- **影響範囲**: 各ジョブ（オーケストレーション）ごと

### 並列処理の動作

オーケストレーターは、音声ファイルをバッチに分割して処理します：

1. 全ファイルを `MaxParallelFiles` のサイズのバッチに分割
2. 各バッチ内のファイルを並列実行（fan-out）
3. バッチの完了を待機（fan-in）
4. 次のバッチを処理

**例**: 12ファイル、MaxParallelFiles=5の場合
- バッチ1: 5ファイル並列実行
- バッチ2: 5ファイル並列実行
- バッチ3: 2ファイル並列実行

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
