"use client";

import { useState } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import ValidationError from "./ValidationError";
import Toast from "./Toast";
import ErrorModal from "./ErrorModal";
import { validateFiles, ValidationError as ValidationErrorType } from "./validation";
import { useUpload } from "./useUpload";

interface ToastState {
  message: string;
  type: "error" | "success" | "info";
}

interface ErrorModalState {
  title: string;
  message: string;
  details?: string;
}

export default function UploadForm() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [validationError, setValidationError] = useState<ValidationErrorType | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [errorModal, setErrorModal] = useState<ErrorModalState | null>(null);
  const { uploadedFiles, isUploading, uploadFiles, resetUpload } = useUpload();

  const handleFilesSelected = (newFiles: File[]) => {
    const updatedFiles = [...selectedFiles, ...newFiles];
    setSelectedFiles(updatedFiles);
    
    // Validate files immediately after selection
    const error = validateFiles(updatedFiles);
    setValidationError(error);
  };

  const handleRemoveFile = (index: number) => {
    const updatedFiles = selectedFiles.filter((_, i) => i !== index);
    setSelectedFiles(updatedFiles);
    
    // Revalidate after removal
    const error = validateFiles(updatedFiles);
    setValidationError(error);
  };

  const handleSubmit = async () => {
    // Validate before submission
    const error = validateFiles(selectedFiles);
    if (error) {
      setValidationError(error);
      return;
    }

    setValidationError(null);

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
          setValidationError(null);
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
          if (firstError.error.toLowerCase().includes("sas") && firstError.error.toLowerCase().includes("expired")) {
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
      // Network error during SAS URL fetch - show toast
      console.error("Upload error:", error);
      const errorMsg = error instanceof Error ? error.message : "アップロードに失敗しました";
      
      // Check if it's a server error (500+) or SAS URL expiration
      if (errorMsg.toLowerCase().includes("sas") && errorMsg.toLowerCase().includes("expired")) {
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
    setValidationError(null);
    setToast(null);
    setErrorModal(null);
    resetUpload();
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      {validationError && (
        <ValidationError message={validationError.message} />
      )}
      
      <FileList files={selectedFiles} onRemoveFile={handleRemoveFile} />

      {selectedFiles.length > 0 && (
        <div className="mt-8 space-y-4">
          <button
            onClick={handleSubmit}
            disabled={isUploading || validationError !== null}
            className={`w-full font-semibold py-4 px-6 rounded-lg transition-colors text-lg ${
              isUploading || validationError
                ? "bg-gray-400 cursor-not-allowed text-gray-200"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
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
