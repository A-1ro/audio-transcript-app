"use client";

import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import useUpload from "./useUpload";

// 状態に応じたメッセージを取得
function getStateMessage(state: string): string {
  switch (state) {
    case "UploadingToBlob":
      return "Blob Storageにアップロード中...";
    case "JobCreated":
      return "ジョブを作成中...";
    case "Processing":
      return "処理を開始しました！";
    default:
      return "";
  }
}

export default function UploadForm() {
  const {
    state,
    selectedFiles,
    errorMessage,
    handleFilesSelected,
    handleRemoveFile,
    handleSubmit,
    isLoading,
  } = useUpload();

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      <FileList files={selectedFiles} onRemoveFile={isLoading ? undefined : handleRemoveFile} />

      {/* エラーメッセージ */}
      {errorMessage && (
        <div className="mt-4 p-4 bg-red-50 border border-red-200 rounded-lg">
          <p className="text-red-800 text-sm">{errorMessage}</p>
        </div>
      )}

      {/* 状態メッセージ */}
      {isLoading && (
        <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
          <div className="flex items-center space-x-3">
            <svg
              className="animate-spin h-5 w-5 text-blue-600"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              ></circle>
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              ></path>
            </svg>
            <p className="text-blue-800 font-medium">{getStateMessage(state)}</p>
          </div>
        </div>
      )}

      {/* 送信ボタン */}
      {selectedFiles.length > 0 && (
        <div className="mt-8">
          <button
            onClick={handleSubmit}
            disabled={isLoading}
            className={`w-full font-semibold py-4 px-6 rounded-lg transition-colors text-lg ${
              isLoading
                ? "bg-gray-400 cursor-not-allowed text-gray-200"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
          >
            {isLoading ? "処理中..." : "アップロードしてジョブ作成"}
          </button>
        </div>
      )}
    </div>
  );
}
