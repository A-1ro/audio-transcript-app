import Link from "next/link";
import UploadForm from "./UploadForm";

export default function UploadPage() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto">
        <div className="mb-8 flex items-center justify-between">
          <Link
            href="/"
            className="text-blue-600 hover:text-blue-800 text-sm font-medium"
          >
            ← ホームに戻る
          </Link>
          <Link
            href="/jobs"
            className="text-blue-600 hover:text-blue-800 text-sm font-medium"
          >
            ジョブ一覧を見る →
          </Link>
        </div>
        
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-2">
            Batch Speech Transcription
          </h1>
          <p className="text-gray-600">
            音声ファイルをアップロードして文字起こしジョブを作成
          </p>
        </div>

        <UploadForm />
      </div>
    </div>
  );
}
