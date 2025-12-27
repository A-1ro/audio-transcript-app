"use client";

import { useState } from "react";

// UI状態の定義
export type UploadState = 
  | "Idle"              // 初期状態
  | "FilesSelected"     // ファイル選択済み
  | "UploadingToBlob"   // Blob Storageへアップロード中
  | "JobCreated"        // ジョブ作成完了
  | "Processing";       // 処理中

export interface UseUploadReturn {
  state: UploadState;
  selectedFiles: File[];
  errorMessage: string | null;
  handleFilesSelected: (newFiles: File[]) => void;
  handleRemoveFile: (index: number) => void;
  handleSubmit: () => Promise<void>;
  isLoading: boolean;
}

export default function useUpload(): UseUploadReturn {
  const [state, setState] = useState<UploadState>("Idle");
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // ファイル選択時の処理
  const handleFilesSelected = (newFiles: File[]) => {
    setSelectedFiles((prevFiles) => [...prevFiles, ...newFiles]);
    if (newFiles.length > 0) {
      setState("FilesSelected");
      setErrorMessage(null);
    }
  };

  // ファイル削除時の処理
  const handleRemoveFile = (index: number) => {
    setSelectedFiles((prevFiles) => {
      const updatedFiles = prevFiles.filter((_, i) => i !== index);
      // ファイルが0件になったらIdleに戻す
      if (updatedFiles.length === 0) {
        setState("Idle");
      }
      return updatedFiles;
    });
  };

  // アップロードとジョブ作成の処理
  const handleSubmit = async () => {
    if (selectedFiles.length === 0) {
      setErrorMessage("ファイルを選択してください");
      return;
    }

    try {
      // Blob Storageへのアップロード開始
      setState("UploadingToBlob");
      setErrorMessage(null);

      // TODO: 実際のアップロード処理を実装
      // 1. SAS URL取得 API呼び出し
      // 2. Blob Storageへアップロード
      await new Promise((resolve) => setTimeout(resolve, 3000)); // シミュレーション

      // ジョブ作成
      setState("JobCreated");
      
      // TODO: 実際のジョブ作成 API呼び出し
      await new Promise((resolve) => setTimeout(resolve, 2000)); // シミュレーション

      // 処理開始
      setState("Processing");
      
      console.log("Upload completed:", selectedFiles);
      
      // 成功後、数秒待ってからIdleに戻す（実際にはジョブ詳細画面への遷移など）
      setTimeout(() => {
        setSelectedFiles([]);
        setState("Idle");
      }, 3000);
      
    } catch (error) {
      setErrorMessage("アップロードに失敗しました。もう一度お試しください。");
      setState("FilesSelected");
      console.error("Upload error:", error);
    }
  };

  // ローディング状態の判定
  const isLoading = state === "UploadingToBlob" || state === "JobCreated" || state === "Processing";

  return {
    state,
    selectedFiles,
    errorMessage,
    handleFilesSelected,
    handleRemoveFile,
    handleSubmit,
    isLoading,
  };
}
