# Issue: UI状態遷移の管理

## 目的
`Idle → FilesSelected → UploadingToBlob → JobCreated → Processing` を管理する。

## 受け入れ条件
- 状態に応じたUIの変化が実装されている
- 送信中はボタンが無効化される

## 参考
- docs/1.upload-ui.md
