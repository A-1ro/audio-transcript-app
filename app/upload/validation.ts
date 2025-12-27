// Validation constants
const ALLOWED_EXTENSIONS = ['.mp3', '.wav', '.m4a'];
const ALLOWED_MIME_TYPES = ['audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/x-wav', 'audio/mp4', 'audio/x-m4a'];
const MAX_FILE_COUNT = 50;
const MAX_TOTAL_SIZE = 1024 * 1024 * 1024; // 1GB in bytes
const MAX_DISPLAY_FILES = 5; // Maximum number of invalid file names to display in error messages

export interface ValidationError {
  type: 'format' | 'count' | 'size' | 'empty';
  message: string;
}

/**
 * Validates if a file has an allowed extension
 */
function hasAllowedExtension(file: File): boolean {
  const fileName = file.name.toLowerCase();
  const lastDotIndex = fileName.lastIndexOf('.');
  if (lastDotIndex === -1) return false;
  
  const extension = fileName.substring(lastDotIndex);
  return ALLOWED_EXTENSIONS.includes(extension);
}

/**
 * Validates if a file has an allowed MIME type
 */
function hasAllowedMimeType(file: File): boolean {
  return ALLOWED_MIME_TYPES.includes(file.type);
}

/**
 * Validates if a file is allowed based on both extension and MIME type
 */
function isFileAllowed(file: File): boolean {
  // Check both extension and MIME type for better security
  // Allow empty MIME type to handle browser inconsistencies
  return hasAllowedExtension(file) && (hasAllowedMimeType(file) || file.type === '');
}

/**
 * Validates a list of files against all validation rules
 * Returns an array of validation errors (empty if valid)
 */
export function validateFiles(files: File[]): ValidationError[] {
  const errors: ValidationError[] = [];

  // Check if file list is empty
  if (files.length === 0) {
    errors.push({
      type: 'empty',
      message: 'ファイルを選択してください'
    });
    return errors;
  }

  // Check file count
  if (files.length > MAX_FILE_COUNT) {
    errors.push({
      type: 'count',
      message: `ファイル数が上限を超えています（最大: ${MAX_FILE_COUNT}ファイル、現在: ${files.length}ファイル）`
    });
  }

  // Check file formats
  const invalidFiles = files.filter(file => !isFileAllowed(file));
  if (invalidFiles.length > 0) {
    const displayFileNames = invalidFiles.slice(0, MAX_DISPLAY_FILES).map(f => f.name);
    const remainingCount = invalidFiles.length - MAX_DISPLAY_FILES;
    
    let fileNames = displayFileNames.join(', ');
    if (remainingCount > 0) {
      fileNames += ` ... 他${remainingCount}ファイル`;
    }
    
    const supportedFormats = ALLOWED_EXTENSIONS.map(ext => ext.replace('.', '')).join(', ');
    errors.push({
      type: 'format',
      message: `対応していないファイル形式が含まれています: ${fileNames}。対応形式: ${supportedFormats}`
    });
  }

  // Check total size
  const totalSize = files.reduce((sum, file) => sum + file.size, 0);
  if (totalSize > MAX_TOTAL_SIZE) {
    const totalSizeGB = (totalSize / (1024 * 1024 * 1024)).toFixed(2);
    errors.push({
      type: 'size',
      message: `合計ファイルサイズが上限を超えています（最大: 1GB、現在: ${totalSizeGB}GB）`
    });
  }

  return errors;
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
