"use client";

import { useState } from "react";

// UI状態の定義
export type UploadState = 
  | "Idle"              // 初期状態
  | "FilesSelected"     // ファイル選択済み
  | "UploadingToBlob"   // Blob Storageへアップロード中
  | "JobCreated"        // ジョブ作成完了
  | "Processing";       // 処理中

interface FileInfo {
  name: string;
  size: number;
  type: string;
}

interface UploadInfo {
  fileName: string;
  uploadUrl: string;
  blobUrl: string;
}

export interface UploadedFile {
  file: File;
  blobUrl: string;
  status: "pending" | "uploading" | "success" | "error";
  error?: string;
}

/**
 * Result interface for the useUpload hook
 */
export interface UseUploadResult {
  /** Current state of the upload workflow */
  state: UploadState;
  /** Array of uploaded files with their status */
  uploadedFiles: UploadedFile[];
  /** Whether files are currently being uploaded */
  isUploading: boolean;
  /** Upload files to Azure Blob Storage and track their progress */
  uploadFiles: (files: File[]) => Promise<UploadedFile[]>;
  /** Reset the upload state and clear all files */
  resetUpload: () => void;
}

/**
 * Helper function to check if an error message indicates an expired SAS URL
 */
export function isSasUrlExpired(errorMessage: string): boolean {
  const lowerErrorMessage = errorMessage.toLowerCase();
  return lowerErrorMessage.includes("sas") && lowerErrorMessage.includes("expired");
}

/**
 * Custom hook for managing file uploads to Azure Blob Storage
 * Handles state transitions through the upload workflow:
 * Idle → FilesSelected → UploadingToBlob → JobCreated → Processing
 */
export function useUpload(): UseUploadResult {
  const [state, setState] = useState<UploadState>("Idle");
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const getSasUrls = async (files: File[]): Promise<UploadInfo[]> => {
    const fileInfos: FileInfo[] = files.map((file) => ({
      name: file.name,
      size: file.size,
      type: file.type,
    }));

    const response = await fetch("/api/uploads/sas", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ files: fileInfos }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.error || "Failed to get SAS URLs");
    }

    const data = await response.json();
    return data.uploads;
  };

  const uploadToBlob = async (
    file: File,
    uploadUrl: string
  ): Promise<void> => {
    try {
      const response = await fetch(uploadUrl, {
        method: "PUT",
        headers: {
          "x-ms-blob-type": "BlockBlob",
          "Content-Type": file.type,
        },
        body: file,
      });

      if (!response.ok) {
        // Check if it's a SAS URL expiration error (403 or specific error codes)
        if (response.status === 403) {
          throw new Error("SAS URL has expired. Please retry the upload.");
        }
        throw new Error(`Upload failed: ${response.statusText}`);
      }
    } catch (error) {
      // Check if it's a network error (like ERR_BLOCKED_BY_CLIENT)
      // This typically occurs in development when using mock URLs
      if (error instanceof TypeError && error.message.includes("Failed to fetch")) {
        // Check if we're using a mock URL (not a real Azure endpoint)
        // Note: In production with real Azure Blob Storage, this mock handling won't be triggered
        if (uploadUrl.includes("mockstorageaccount")) {
          console.warn("Upload blocked - using mock URL in development mode");
          // In development with mock URLs, treat this as success
          return;
        }
        // For real URLs, this is a genuine network error
        throw new Error("Network error during upload. Please check your connection.");
      }
      throw error;
    }
  };

  const uploadFiles = async (files: File[]): Promise<UploadedFile[]> => {
    setIsUploading(true);
    setState("UploadingToBlob");

    try {
      // Initialize uploaded files state
      const initialFiles: UploadedFile[] = files.map((file) => ({
        file,
        blobUrl: "",
        status: "pending",
      }));
      setUploadedFiles(initialFiles);

      // Get SAS URLs
      const uploadInfos = await getSasUrls(files);

      // Validate that we got SAS URLs for all files
      if (uploadInfos.length !== files.length) {
        throw new Error(
          `Expected ${files.length} SAS URLs but got ${uploadInfos.length}`
        );
      }

      // Upload each file
      const results: UploadedFile[] = [];
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const uploadInfo = uploadInfos[i];

        try {
          // Update status to uploading
          setUploadedFiles((prev) =>
            prev.map((f, idx) =>
              idx === i ? { ...f, status: "uploading" } : f
            )
          );

          // Upload to blob storage
          await uploadToBlob(file, uploadInfo.uploadUrl);

          // Update status to success
          const successFile: UploadedFile = {
            file,
            status: "success",
            blobUrl: uploadInfo.blobUrl,
          };
          results.push(successFile);
          setUploadedFiles((prev) =>
            prev.map((f, idx) =>
              idx === i
                ? { ...f, status: "success", blobUrl: uploadInfo.blobUrl }
                : f
            )
          );
        } catch (error) {
          // Update status to error
          const errorMessage =
            error instanceof Error ? error.message : "Upload failed";
          const errorFile: UploadedFile = {
            file,
            status: "error",
            blobUrl: "",
            error: errorMessage,
          };
          results.push(errorFile);
          setUploadedFiles((prev) =>
            prev.map((f, idx) =>
              idx === i ? { ...f, status: "error", error: errorMessage } : f
            )
          );

          // If SAS URL expired, throw error to allow retry
          if (isSasUrlExpired(errorMessage)) {
            throw error;
          }
        }
      }
      
      // Transition to JobCreated state (files uploaded, ready for job creation)
      setState("JobCreated");
      
      return results;
    } catch (error) {
      // On error, check if any files were successfully uploaded
      const hasSuccessfulUploads = uploadedFiles.some(f => f.status === "success");
      
      // If files were partially uploaded, keep the JobCreated state to allow retry
      // Otherwise, return to FilesSelected state
      if (hasSuccessfulUploads) {
        setState("JobCreated");
      } else {
        setState("FilesSelected");
      }
      throw error;
    } finally {
      setIsUploading(false);
    }
  };

  const resetUpload = () => {
    setUploadedFiles([]);
    setIsUploading(false);
    setState("Idle");
  };

  return {
    state,
    uploadedFiles,
    isUploading,
    uploadFiles,
    resetUpload,
  };
}
