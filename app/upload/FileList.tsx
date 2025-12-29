"use client";

import { formatFileSize } from "./validation";

interface FileListProps {
  files: File[];
  onRemoveFile?: (index: number) => void;
}

export default function FileList({ files, onRemoveFile }: FileListProps) {
  if (files.length === 0) {
    return null;
  }

  return (
    <div className="mt-8">
      <h3 className="text-lg font-semibold mb-4 text-soft-text">Selected Files</h3>
      <div className="space-y-3">
        {files.map((file, index) => (
          <div
            key={`${file.name}-${index}`}
            className="flex items-center justify-between p-4 bg-white border border-soft-border rounded-xl shadow-soft-sm hover:shadow-soft transition-shadow duration-200"
          >
            <div className="flex items-center space-x-4 flex-1 min-w-0">
              <div className="h-10 w-10 bg-soft-bg rounded-lg flex items-center justify-center flex-shrink-0">
                <svg
                  className="h-6 w-6 text-soft-primary"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3"
                  />
                </svg>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-soft-text truncate">
                  {file.name}
                </p>
                <p className="text-xs text-soft-subtext mt-0.5">
                  {formatFileSize(file.size)}
                </p>
              </div>
            </div>
            {onRemoveFile && (
              <button
                onClick={() => onRemoveFile(index)}
                className="ml-4 text-soft-subtext hover:text-red-500 flex-shrink-0 transition-colors bg-transparent hover:bg-red-50 p-2 rounded-full"
                aria-label={`Remove ${file.name}`}
              >
                <svg
                  className="h-5 w-5"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            )}
          </div>
        ))}
      </div>
      <div className="mt-4 text-sm text-soft-subtext text-right font-medium">
        Total: {files.length} files ({formatFileSize(files.reduce((sum, file) => sum + file.size, 0))})
      </div>
    </div>
  );
}
