"use client";

import { useState } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import { useUpload } from "./useUpload";

export default function UploadForm() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [successMessage, setSuccessMessage] = useState<string>("");
  const { uploadedFiles, isUploading, uploadFiles, resetUpload } = useUpload();

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

      // Check if SAS URL expired
      const isSasExpired = errorMsg.toLowerCase().includes("sas") && errorMsg.toLowerCase().includes("expired");
      if (isSasExpired) {
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

      <FileList files={selectedFiles} onRemoveFile={handleRemoveFile} />

      {selectedFiles.length > 0 && (
        <div className="mt-8 space-y-4">
          <button
            onClick={handleSubmit}
            disabled={isUploading}
            className="w-full bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed text-white font-semibold py-4 px-6 rounded-lg transition-colors text-lg"
          >
            {isUploading ? "アップロード中..." : "アップロードしてジョブ作成"}
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

      {isUploading && (
        <div className="mt-4">
          <div className="flex items-center justify-center space-x-2">
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600"></div>
            <span className="text-sm text-gray-600">
              ファイルをアップロード中...
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
