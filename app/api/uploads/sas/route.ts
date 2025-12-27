import { NextRequest, NextResponse } from "next/server";

interface FileInfo {
  name: string;
  size: number;
  type: string;
}

interface RequestBody {
  files: FileInfo[];
}

interface UploadInfo {
  fileName: string;
  uploadUrl: string;
  blobUrl: string;
}

// Maximum number of files allowed per upload
const MAX_FILES = 50;

// Maximum file name length
const MAX_FILENAME_LENGTH = 255;

/**
 * Validate file name for security and compatibility
 */
function validateFileName(fileName: string): { valid: boolean; error?: string } {
  if (!fileName || fileName.trim().length === 0) {
    return { valid: false, error: "File name cannot be empty" };
  }

  if (fileName.length > MAX_FILENAME_LENGTH) {
    return { valid: false, error: `File name too long (max ${MAX_FILENAME_LENGTH} characters)` };
  }

  // Check for potentially dangerous characters
  const dangerousChars = /[<>:"|?*\x00-\x1f]/;
  if (dangerousChars.test(fileName)) {
    return { valid: false, error: "File name contains invalid characters" };
  }

  return { valid: true };
}

/**
 * POST /api/uploads/sas
 * Generate SAS URLs for uploading files to Azure Blob Storage
 */
export async function POST(request: NextRequest) {
  try {
    const body: RequestBody = await request.json();

    if (!body.files || !Array.isArray(body.files)) {
      return NextResponse.json(
        { error: "Invalid request: files array is required" },
        { status: 400 }
      );
    }

    if (body.files.length === 0) {
      return NextResponse.json(
        { error: "At least one file is required" },
        { status: 400 }
      );
    }

    // Validate maximum file count
    if (body.files.length > MAX_FILES) {
      return NextResponse.json(
        { error: `Too many files. Maximum ${MAX_FILES} files allowed per upload` },
        { status: 400 }
      );
    }

    // Validate each file
    for (const file of body.files) {
      const validation = validateFileName(file.name);
      if (!validation.valid) {
        return NextResponse.json(
          { error: `Invalid file name "${file.name}": ${validation.error}` },
          { status: 400 }
        );
      }
    }

    // TODO: Replace with actual Azure Blob Storage integration
    // For now, generate mock URLs for development
    const uploads: UploadInfo[] = body.files.map((file) => {
      const timestamp = Date.now();
      const sanitizedFileName = encodeURIComponent(file.name);
      
      // Mock SAS URL with expiration time (1 hour from now)
      const expirationTime = new Date(Date.now() + 60 * 60 * 1000).toISOString();
      const mockSasToken = `sv=2023-01-01&se=${expirationTime}&sr=b&sp=cw&sig=mock_signature_${timestamp}`;
      
      // Mock Azure Blob Storage URLs
      const blobUrl = `https://mockstorageaccount.blob.core.windows.net/audio-files/${timestamp}-${sanitizedFileName}`;
      const uploadUrl = `${blobUrl}?${mockSasToken}`;

      return {
        fileName: file.name,
        uploadUrl,
        blobUrl,
      };
    });

    return NextResponse.json({
      uploads,
    });
  } catch (error) {
    // Log detailed error for debugging
    console.error("Error generating SAS URLs:", error);
    
    // Return generic error to client
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}
