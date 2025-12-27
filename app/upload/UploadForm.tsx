"use client";

import { useState } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";

export default function UploadForm() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);

  const handleFilesSelected = (newFiles: File[]) => {
    setSelectedFiles((prevFiles) => [...prevFiles, ...newFiles]);
  };

  const handleRemoveFile = (index: number) => {
    setSelectedFiles((prevFiles) => prevFiles.filter((_, i) => i !== index));
  };

  const handleSubmit = () => {
    if (selectedFiles.length === 0) {
      alert("ファイルを選択してください");
      return;
    }

    // TODO: Implement actual upload and job creation logic
    console.log("Uploading files:", selectedFiles);
    alert(`${selectedFiles.length} ファイルをアップロードしてジョブを作成します`);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      <FileList files={selectedFiles} onRemoveFile={handleRemoveFile} />

      {selectedFiles.length > 0 && (
        <div className="mt-8">
          <button
            onClick={handleSubmit}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-4 px-6 rounded-lg transition-colors text-lg"
          >
            アップロードしてジョブ作成
          </button>
        </div>
      )}
    </div>
  );
}
