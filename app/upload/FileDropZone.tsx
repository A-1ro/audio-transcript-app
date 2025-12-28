"use client";

import { DragEvent, ChangeEvent, useRef } from "react";

interface FileDropZoneProps {
  onFilesSelected: (files: File[]) => void;
}

export default function FileDropZone({ onFilesSelected }: FileDropZoneProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();

    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) {
      onFilesSelected(files);
    }
  };

  const handleFileInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      onFilesSelected(Array.from(files));
    }
  };

  const handleClick = () => {
    fileInputRef.current?.click();
  };

  return (
    <div
      onClick={handleClick}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      className="group relative border-2 border-dashed border-soft-border rounded-xl p-12 text-center cursor-pointer hover:border-soft-primary hover:bg-soft-bg transition-all duration-200 ease-in-out"
    >
      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept="audio/*,.mp3,.wav,.m4a"
        onChange={handleFileInputChange}
        className="hidden"
      />
      <div className="space-y-4">
        <div className="mx-auto h-16 w-16 bg-soft-bg rounded-full flex items-center justify-center group-hover:bg-white transition-colors">
            <svg
            className="h-8 w-8 text-soft-subtext group-hover:text-soft-primary transition-colors"
            stroke="currentColor"
            fill="none"
            viewBox="0 0 48 48"
            aria-hidden="true"
            >
            <path
                d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02"
                strokeWidth={2}
                strokeLinecap="round"
                strokeLinejoin="round"
            />
            </svg>
        </div>
        <div className="text-soft-text">
          <span className="font-semibold text-soft-primary text-lg">Click to select files</span>
          <span className="block mt-1 text-soft-subtext">or drag and drop audio files here</span>
        </div>
        <div className="text-sm text-soft-subtext/80 space-y-1">
          <p>Supported: mp3 / wav / m4a</p>
          <p>Max: 50 files / 1GB total</p>
        </div>
      </div>
    </div>
  );
}
