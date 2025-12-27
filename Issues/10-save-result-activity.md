# Issue: SaveResultActivity 実装（Cosmos DB）

## 目的
文字起こし結果を永続化する。

## 受け入れ条件
- Transcriptions を Cosmos DB に保存できる
- 既存結果がある場合は二重登録しない
- 必要に応じて raw 結果を Blob に保存できる

## 参考
- docs/2.functions-transcription-design.md
