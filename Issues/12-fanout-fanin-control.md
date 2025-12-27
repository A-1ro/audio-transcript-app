# Issue: 並列処理（fan-out / fan-in）制御

## 目的
音声ファイル単位で並列実行する。

## 受け入れ条件
- Durable Functions の fan-out / fan-in パターンを適用
- 並列実行数を制御できる（必要なら設定化）

## 参考
- docs/2.functions-transcription-design.md
