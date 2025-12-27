# Issue: ジョブ一覧（簡易）画面の表示（U-02）

## 目的
作成済みジョブの一覧を表示する。

## 受け入れ条件
- `GET /api/jobs` の結果を一覧表示
- `jobId / status / createdAt` が表示される
- 一覧が空の状態も考慮する
- データソースは Azure Cosmos DB（バックエンド）であることを前提にする

## 参考
- docs/1.upload-ui.md
