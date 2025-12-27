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

### 開発サーバーの起動

```bash
npm run dev
```

ブラウザで [http://localhost:3000](http://localhost:3000) を開いてください。

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

## 今後の実装予定

- クライアントサイドバリデーション
- Azure Blob Storage へのアップロード
- ジョブ作成API連携
- ジョブ一覧画面
- 認証機能

## ドキュメント

詳細な設計ドキュメントは `docs/` ディレクトリを参照してください。

- [1.upload-ui.md](docs/1.upload-ui.md) - 音声アップロードUI設計書

## ライセンス

このプロジェクトはポートフォリオ用のサンプルアプリケーションです。
