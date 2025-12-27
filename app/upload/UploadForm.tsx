"use client";

import { useState, useMemo } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import { useUpload, type UploadState, isSasUrlExpired } from "./useUpload";
import { validateFiles } from "./validation";

// 状態に応じたメッセージを取得
function getStateMessage(state: UploadState): string {
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
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [successMessage, setSuccessMessage] = useState<string>("");
  const { state, uploadedFiles, isUploading, uploadFiles, resetUpload } = useUpload();

  const handleFilesSelected = (newFiles: File[]) => {
    setSelectedFiles((prevFiles) => [...prevFiles, ...newFiles]);
    setErrorMessage("");
    setSuccessMessage("");
  };

  const handleRemoveFile = (index: number) => {
    setSelectedFiles((prevFiles) => prevFiles.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
    if (selectedFiles.length === 0) {
      setErrorMessage("ファイルを選択してください");
      return;
    }

    setErrorMessage("");
    setSuccessMessage("");

    try {
      const results = await uploadFiles(selectedFiles);

      // Check if all uploads succeeded
      const successCount = results.filter((r) => r.status === "success").length;
      if (successCount > 0) {
        setSuccessMessage(
          `${successCount} ファイルのアップロードが完了しました`
        );
        // Store blob URLs for job creation
        const blobUrls = results
          .filter((f) => f.status === "success")
          .map((f) => ({
            fileName: f.file.name,
            blobUrl: f.blobUrl,
          }));
        console.log("Uploaded files:", blobUrls);
      }
    } catch (error) {
      const errorMsg =
        error instanceof Error ? error.message : "アップロードに失敗しました";

      // Check if SAS URL expired using the helper function
      if (isSasUrlExpired(errorMsg)) {
        setErrorMessage(
          "アップロードURLの有効期限が切れました。もう一度アップロードしてください。"
        );
      } else {
        setErrorMessage(errorMsg);
      }
    }
  };

  const handleReset = () => {
    setSelectedFiles([]);
    setErrorMessage("");
    setSuccessMessage("");
    resetUpload();
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />

      {errorMessage && (
        <div className="mt-4 p-4 bg-red-50 border border-red-200 rounded-lg">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg
                className="h-5 w-5 text-red-400"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-red-800">{errorMessage}</p>
            </div>
          </div>
        </div>
      )}

      {successMessage && (
        <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-lg">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg
                className="h-5 w-5 text-green-400"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-green-800">
                {successMessage}
              </p>
            </div>
          </div>
        </div>
      )}

      <FileList files={selectedFiles} onRemoveFile={isUploading ? undefined : handleRemoveFile} />

      {/* 状態メッセージ */}
      {isUploading && (
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
        <div className="mt-8 space-y-4">
          <button
            onClick={handleSubmit}
            disabled={isUploading}
            className={`w-full font-semibold py-4 px-6 rounded-lg transition-colors text-lg ${
              isUploading
                ? "bg-gray-400 cursor-not-allowed text-gray-200"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
          >
            {isUploading ? "処理中..." : "アップロードしてジョブ作成"}
          </button>

          {uploadedFiles.length > 0 && (
            <button
              onClick={handleReset}
              disabled={isUploading}
              className="w-full bg-gray-200 hover:bg-gray-300 disabled:bg-gray-100 disabled:cursor-not-allowed text-gray-700 font-semibold py-3 px-6 rounded-lg transition-colors"
            >
              リセット
            </button>
          )}
        </div>
      )}
    </div>
  );
}
