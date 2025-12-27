# Issue: Azure Blob Storage へのアップロード実装（SAS URL）

## 目的
音声ファイルをクライアントから Azure Blob Storage にアップロードする。

## 受け入れ条件
- `POST /api/uploads/sas` でアップロード用URLを取得できる
- 取得した `uploadUrl` へファイルをアップロードできる
- 成功時に `blobUrl` を保持し、後続のジョブ作成で利用できる
- SAS URL の期限切れ時に再取得の導線がある

## 参考
- docs/1.upload-ui.md
