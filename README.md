# Batch Speech Transcription App

音声ファイルのバッチ文字起こしアプリケーション

## 概要

複数の音声ファイルをアップロードし、バッチで文字起こし処理を行うWebアプリケーションです。

## 技術スタック

- **フレームワーク**: Next.js 15 (App Router)
- **言語**: TypeScript
- **スタイリング**: Tailwind CSS
- **実行環境**: Node.js 20+

## セットアップ

### 依存関係のインストール

```bash
npm install
```

### 環境変数の設定

`.env.local.template` をコピーして `.env.local` を作成し、必要な環境変数を設定してください。

```bash
cp .env.local.template .env.local
```

#### 必須の環境変数

- `AZURE_FUNCTIONS_URL`: Azure Functions API の URL
  - ローカル開発: `http://localhost:7071`
  - 本番環境: デプロイ済みの Azure Functions アプリの URL
- `AZURE_FUNCTIONS_API_KEY`: Azure Functions の Function レベル API キー
  - 本番環境では必須。Azure Portal の対象 Function App の「アプリケーション設定」や「関数キー」から取得してください。
  - ローカル開発では、`local.settings.json` の `Values` セクションまたは Azure Functions Core Tools の起動ログに表示される `?code=...` の値を使用してください。

### 開発サーバーの起動

#### Next.js フロントエンド

```bash
npm run dev
```

ブラウザで [http://localhost:3000](http://localhost:3000) を開いてください。

#### Azure Functions バックエンド（オプション）

ジョブ一覧などの機能を完全に動作させるには、Azure Functions を起動する必要があります。

```bash
cd TranscriptionFunctions
func start
```

Azure Functions は `http://localhost:7071` で起動します。

### ビルド

```bash
npm run build
```

### 本番環境での起動

```bash
npm start
```

## プロジェクト構成

```
/app
  ├── layout.tsx          # ルートレイアウト
  ├── page.tsx            # ホームページ
  ├── globals.css         # グローバルスタイル
  └── upload/             # アップロード機能
      ├── page.tsx        # アップロードページ
      ├── UploadForm.tsx  # アップロードフォーム
      ├── FileDropZone.tsx # ファイル選択UI
      └── FileList.tsx    # ファイル一覧表示
```

## 実装済み機能

### U-01: 音声アップロード画面

- ✅ Drag & Drop によるファイル選択
- ✅ File Picker によるファイル選択
- ✅ 選択済みファイル一覧表示（ファイル名とサイズ）
- ✅ アップロードボタン
- ✅ ファイルの削除機能

対応形式: mp3 / wav / m4a
最大: 50 ファイル / 合計 1GB

### U-02: ジョブ一覧画面

- ✅ Azure Cosmos DB からのジョブ一覧取得
- ✅ JobId / Status / CreatedAt の表示
- ✅ ステータス別の色分け表示
- ✅ 空の状態の表示

## 今後の実装予定

- クライアントサイドバリデーション
- Azure Blob Storage へのアップロード
- ジョブ作成API連携
- 認証機能
- ジョブ詳細画面

## ドキュメント

詳細な設計ドキュメントは `docs/` ディレクトリを参照してください。

- [1.upload-ui.md](docs/1.upload-ui.md) - 音声アップロードUI設計書

## ライセンス

このプロジェクトはポートフォリオ用のサンプルアプリケーションです。
