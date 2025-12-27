# Issue: ジョブ起動トリガー（Queue）実装

## 目的
ジョブ作成後、QueueからDurable Orchestratorを起動する。

## 受け入れ条件
- Queueメッセージを受け取り `TranscriptionOrchestrator` を開始できる
- JobId を入力として渡せる
- 失敗時のログが出力される

## 参考
- docs/2.functions-transcription-design.md
