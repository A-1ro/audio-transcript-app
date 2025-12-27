"use client";

import { useState } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import ValidationError from "./ValidationError";
import Toast from "./Toast";
import ErrorModal from "./ErrorModal";
import { validateFiles, type ValidationError as ValidationErrorType } from "./validation";
import { useUpload, type UploadState, isSasUrlExpired } from "./useUpload";

interface ToastState {
  message: string;
  type: "error" | "success" | "info";
}

interface ErrorModalState {
  title: string;
  message: string;
  details?: string;
}

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
  const [validationErrors, setValidationErrors] = useState<ValidationErrorType[]>([]);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [errorModal, setErrorModal] = useState<ErrorModalState | null>(null);
  const { state, uploadedFiles, isUploading, uploadFiles, resetUpload } = useUpload();

  const handleFilesSelected = (newFiles: File[]) => {
    const updatedFiles = [...selectedFiles, ...newFiles];
    setSelectedFiles(updatedFiles);
    
    // Validate files immediately after selection
    const errors = validateFiles(updatedFiles);
    setValidationErrors(errors);
  };

  const handleRemoveFile = (index: number) => {
    const updatedFiles = selectedFiles.filter((_, i) => i !== index);
    setSelectedFiles(updatedFiles);
    
    // Revalidate after removal
    const errors = validateFiles(updatedFiles);
    setValidationErrors(errors);
  };

  const handleSubmit = async () => {
    // Validate before submission
    const errors = validateFiles(selectedFiles);
    if (errors.length > 0) {
      setValidationErrors(errors);
      return;
    }

    setValidationErrors([]);

    try {
      const results = await uploadFiles(selectedFiles);

      // Check if all uploads succeeded
      const successCount = results.filter((r) => r.status === "success").length;
      const errorCount = results.filter((r) => r.status === "error").length;

      if (successCount > 0 && errorCount === 0) {
        // All uploads successful - show toast and proceed to job creation
        const blobUrls = results
          .filter((f) => f.status === "success")
          .map((f) => ({
            fileName: f.file.name,
            blobUrl: f.blobUrl,
          }));

        // Create job
        try {
          const jobResponse = await fetch("/api/jobs", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              audioFiles: blobUrls,
            }),
          });

          if (!jobResponse.ok) {
            const errorData = await jobResponse.json().catch(() => ({}));
            
            if (jobResponse.status >= 500) {
              // Server error - show modal
              setErrorModal({
                title: "サーバーエラー",
                message: "ジョブの作成中にサーバーで問題が発生しました。",
                details: `ステータスコード: ${jobResponse.status}`,
              });
            } else {
              setErrorModal({
                title: "ジョブ作成エラー",
                message: errorData.message || "ジョブの作成に失敗しました。",
                details: `ステータスコード: ${jobResponse.status}`,
              });
            }
            return;
          }

          const { jobId } = await jobResponse.json();

          // Success - show toast
          setToast({
            message: `ジョブを作成しました (ID: ${jobId})`,
            type: "success",
          });
          
          // Reset form
          setSelectedFiles([]);
          setValidationErrors([]);
          resetUpload();
        } catch (jobError) {
          // Network error during job creation - show toast
          console.error("Job creation error:", jobError);
          setToast({
            message: "ジョブ作成時に通信に失敗しました。ネットワーク接続を確認してください。",
            type: "error",
          });
        }
      } else if (errorCount > 0) {
        // Some uploads failed - show error details
        const firstError = results.find((r) => r.status === "error");
        if (firstError && firstError.error) {
          // Check for specific error types
          if (isSasUrlExpired(firstError.error)) {
            setErrorModal({
              title: "SAS URL の有効期限切れ",
              message: "署名 URL の有効期限が切れています。ページを更新して再度お試しください。",
              details: firstError.error,
            });
          } else {
            setErrorModal({
              title: "アップロードエラー",
              message: `${errorCount} ファイルのアップロードに失敗しました。`,
              details: firstError.error,
            });
          }
        }
      }
    } catch (error) {
      // Network error during SAS URL fetch or upload - handle appropriately
      console.error("Upload error:", error);
      const errorMsg = error instanceof Error ? error.message : "アップロードに失敗しました";
      
      // Check if it's a SAS URL expiration
      if (isSasUrlExpired(errorMsg)) {
        setErrorModal({
          title: "SAS URL の有効期限切れ",
          message: "署名 URL の有効期限が切れています。ページを更新して再度お試しください。",
          details: errorMsg,
        });
      } else if (errorMsg.toLowerCase().includes("failed to get sas")) {
        setErrorModal({
          title: "エラーが発生しました",
          message: "SAS URL の取得に失敗しました。",
          details: errorMsg,
        });
      } else {
        // Network error - show toast
        setToast({
          message: "通信に失敗しました。ネットワーク接続を確認してください。",
          type: "error",
        });
      }
    }
  };

  const handleRetrySubmit = async () => {
    setErrorModal(null);
    await handleSubmit();
  };

  const handleReset = () => {
    setSelectedFiles([]);
    setValidationErrors([]);
    setToast(null);
    setErrorModal(null);
    resetUpload();
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      {/* Display all validation errors */}
      {validationErrors.length > 0 && (
        <div className="space-y-2 mt-4">
          {validationErrors.map((error, index) => (
            <ValidationError key={`${error.type}-${index}`} message={error.message} />
          ))}
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
            disabled={isUploading || validationErrors.length > 0}
            className={`w-full font-semibold py-4 px-6 rounded-lg transition-colors text-lg ${
              isUploading || validationErrors.length > 0
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

      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}

      {errorModal && (
        <ErrorModal
          title={errorModal.title}
          message={errorModal.message}
          details={errorModal.details}
          onClose={() => setErrorModal(null)}
          onRetry={handleRetrySubmit}
        />
      )}
    </div>
  );
}
