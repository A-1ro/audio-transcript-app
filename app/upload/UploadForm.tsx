"use client";

import { useState } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import ValidationError from "./ValidationError";
import Toast from "./Toast";
import ErrorModal from "./ErrorModal";
import { validateFiles, ValidationError as ValidationErrorType } from "./validation";

interface ToastState {
  message: string;
  type: "error" | "success" | "info";
}

interface ErrorModalState {
  title: string;
  message: string;
  details?: string;
}

interface UploadInfo {
  fileName: string;
  uploadUrl: string;
  blobUrl: string;
}

export default function UploadForm() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [validationError, setValidationError] = useState<ValidationErrorType | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [errorModal, setErrorModal] = useState<ErrorModalState | null>(null);
  const [isUploading, setIsUploading] = useState(false);

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

    setIsUploading(true);
    setValidationError(null);

    try {
      // Step 1: Get SAS URLs
      const sasResponse = await fetch("/api/uploads/sas", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          files: selectedFiles.map(f => ({
            name: f.name,
            size: f.size,
            type: f.type,
          })),
        }),
      });

      if (!sasResponse.ok) {
        // Check for SAS URL expiration or server errors
        const errorData = await sasResponse.json().catch(() => ({}));
        
        if (sasResponse.status === 403 && errorData.code === "SAS_EXPIRED") {
          // SAS URL expired - show modal with guidance
          setErrorModal({
            title: "SAS URL の有効期限切れ",
            message: "署名 URL の有効期限が切れています。ページを更新して再度お試しください。",
            details: errorData.message,
          });
        } else if (sasResponse.status >= 500) {
          // Server error - show modal
          setErrorModal({
            title: "サーバーエラー",
            message: "サーバーで問題が発生しました。しばらく待ってから再度お試しください。",
            details: `ステータスコード: ${sasResponse.status}`,
          });
        } else {
          // Other errors - show modal
          setErrorModal({
            title: "エラーが発生しました",
            message: errorData.message || "SAS URL の取得に失敗しました。",
            details: `ステータスコード: ${sasResponse.status}`,
          });
        }
        setIsUploading(false);
        return;
      }

      const { uploads } = await sasResponse.json();

      // Step 2: Upload files to blob storage
      const uploadPromises = uploads.map(async (upload: UploadInfo, index: number) => {
        const file = selectedFiles[index];
        const response = await fetch(upload.uploadUrl, {
          method: "PUT",
          headers: {
            "x-ms-blob-type": "BlockBlob",
            "Content-Type": file.type,
          },
          body: file,
        });

        if (!response.ok) {
          throw new Error(`Failed to upload ${file.name}: ${response.status} ${response.statusText}`);
        }

        return { fileName: file.name, blobUrl: upload.blobUrl };
      });

      const uploadedFiles = await Promise.all(uploadPromises);

      // Step 3: Create job
      const jobResponse = await fetch("/api/jobs", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          audioFiles: uploadedFiles,
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
        setIsUploading(false);
        return;
      }

      const { jobId } = await jobResponse.json();

      // Success
      setToast({
        message: `ジョブを作成しました (ID: ${jobId})`,
        type: "success",
      });
      
      // Reset form
      setSelectedFiles([]);
      setValidationError(null);
    } catch (error) {
      // Network error - show toast
      console.error("Upload error:", error);
      setToast({
        message: "通信に失敗しました。ネットワーク接続を確認してください。",
        type: "error",
      });
    } finally {
      setIsUploading(false);
    }
  };

  const handleRetrySubmit = () => {
    setErrorModal(null);
    handleSubmit();
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      {validationError && (
        <ValidationError message={validationError.message} />
      )}
      
      <FileList files={selectedFiles} onRemoveFile={handleRemoveFile} />

      {selectedFiles.length > 0 && (
        <div className="mt-8">
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
