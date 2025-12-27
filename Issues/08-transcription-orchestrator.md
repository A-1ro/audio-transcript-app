# Issue: TranscriptionOrchestrator 実装

## 目的
ジョブ単位の処理全体を制御する。

## 受け入れ条件
- ジョブ情報取得 → 音声一覧取得 → Activity fan-out → 結果集約 → 状態更新の流れを持つ
- 一部失敗時に PartiallyFailed へ遷移できる
- RetryOptions が設定されている

## 参考
- docs/2.functions-transcription-design.md
