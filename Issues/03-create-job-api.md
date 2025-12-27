# Issue: ジョブ作成API呼び出し（POST /api/jobs）

## 目的
Blob Storage にアップロード済みのファイル情報をジョブ作成APIへ送信する。

## 受け入れ条件
- `audioFiles: { fileName, blobUrl }[]` を含むリクエストを送る
- 成功時に `jobId` と `status` を受け取れる
- 送信中の状態（JobCreated 直前）をUIで表現できる

## 参考
- docs/1.upload-ui.md
