"use client";

import { useState, useEffect } from "react";
import FileDropZone from "./FileDropZone";
import FileList from "./FileList";
import { validateFiles, ValidationError } from "./validation";

export default function UploadForm() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);

  // Validate files whenever they change
  useEffect(() => {
    const errors = validateFiles(selectedFiles);
    setValidationErrors(errors);
  }, [selectedFiles]);

  const handleFilesSelected = (newFiles: File[]) => {
    setSelectedFiles((prevFiles) => [...prevFiles, ...newFiles]);
  };

  const handleRemoveFile = (index: number) => {
    setSelectedFiles((prevFiles) => prevFiles.filter((_, i) => i !== index));
  };

  const handleSubmit = () => {
    // Validate one more time on submit
    const errors = validateFiles(selectedFiles);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    // TODO: Implement actual upload and job creation logic
    console.log("Uploading files:", selectedFiles);
    alert(`${selectedFiles.length} ファイルをアップロードしてジョブを作成します`);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <FileDropZone onFilesSelected={handleFilesSelected} />
      
      {/* Display validation errors inline */}
      {validationErrors.length > 0 && (
        <div className="mt-6 space-y-2">
          {validationErrors.map((error, index) => (
            <div
              key={`${error.type}-${index}`}
              className="p-4 bg-red-50 border border-red-200 rounded-lg"
            >
              <div className="flex items-start">
                <svg
                  className="h-5 w-5 text-red-600 mt-0.5 mr-3 flex-shrink-0"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                  />
                </svg>
                <p className="text-sm text-red-800">{error.message}</p>
              </div>
            </div>
          ))}
        </div>
      )}
      
      <FileList files={selectedFiles} onRemoveFile={handleRemoveFile} />

      {selectedFiles.length > 0 && (
        <div className="mt-8">
          <button
            onClick={handleSubmit}
            disabled={validationErrors.length > 0}
            className={`w-full font-semibold py-4 px-6 rounded-lg transition-colors text-lg ${
              validationErrors.length > 0
                ? "bg-gray-300 text-gray-500 cursor-not-allowed"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
          >
            アップロードしてジョブ作成
          </button>
        </div>
      )}
    </div>
  );
}
