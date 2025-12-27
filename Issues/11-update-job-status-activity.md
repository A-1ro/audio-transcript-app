# Issue: UpdateJobStatusActivity 実装（Cosmos DB）

## 目的
ジョブ状態と時刻を更新する。

## 受け入れ条件
- Jobs の `status / startedAt / finishedAt` を更新できる
- 状態遷移（Pending→Processing→Completed/Failed）が整合する

## 参考
- docs/2.functions-transcription-design.md
