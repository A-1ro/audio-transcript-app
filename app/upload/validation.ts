export interface ValidationError {
  type: "format" | "count" | "size" | "empty";
  message: string;
}

const MAX_FILES = 50;
const MAX_TOTAL_SIZE = 1024 * 1024 * 1024; // 1GB in bytes
const ALLOWED_EXTENSIONS = ["mp3", "wav", "m4a"];
const ALLOWED_MIME_TYPES = ["audio/mpeg", "audio/wav", "audio/x-wav", "audio/mp4", "audio/x-m4a"];

/**
 * Validate file format
 */
function validateFileFormat(file: File): boolean {
  // Check MIME type
  if (file.type && ALLOWED_MIME_TYPES.includes(file.type)) {
    return true;
  }

  // Fallback to extension check
  const lastDotIndex = file.name.lastIndexOf(".");
  if (lastDotIndex === -1) return false;
  
  const extension = file.name.substring(lastDotIndex + 1).toLowerCase();
  return ALLOWED_EXTENSIONS.includes(extension);
}

/**
 * Validate selected files according to requirements
 */
export function validateFiles(files: File[]): ValidationError | null {
  // Check if files are empty
  if (files.length === 0) {
    return {
      type: "empty",
      message: "ファイルを選択してください",
    };
  }

  // Check file count
  if (files.length > MAX_FILES) {
    return {
      type: "count",
      message: `ファイル数が制限を超えています（最大: ${MAX_FILES}ファイル、選択中: ${files.length}ファイル）`,
    };
  }

  // Check file formats
  const invalidFiles = files.filter(file => !validateFileFormat(file));
  if (invalidFiles.length > 0) {
    return {
      type: "format",
      message: `対応していない形式のファイルが含まれています: ${invalidFiles.map(f => f.name).join(", ")}。対応形式: mp3 / wav / m4a`,
    };
  }

  // Check total size
  const totalSize = files.reduce((sum, file) => sum + file.size, 0);
  if (totalSize > MAX_TOTAL_SIZE) {
    const totalSizeGB = (totalSize / (1024 * 1024 * 1024)).toFixed(2);
    return {
      type: "size",
      message: `ファイルの合計サイズが制限を超えています（最大: 1GB、現在: ${totalSizeGB}GB）`,
    };
  }

  return null;
}

/**
 * Format file size for display
 */
export function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const sizes = ["Bytes", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return (bytes / Math.pow(k, i)).toFixed(2) + " " + sizes[i];
}
