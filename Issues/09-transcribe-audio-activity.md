# Issue: TranscribeAudioActivity 実装

## 目的
音声1件の文字起こしを実行する。

## 受け入れ条件
- 入力: `JobId / FileId / BlobUrl`
- Azure AI Speech Service（Batch）に送信できる
- 出力: `FileId / TranscriptText / Confidence / Status`
- 失敗時はステータスを Failed として返す

## 参考
- docs/2.functions-transcription-design.md
