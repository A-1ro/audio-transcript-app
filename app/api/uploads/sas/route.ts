import { NextRequest, NextResponse } from "next/server";
import {
  BlobServiceClient,
  StorageSharedKeyCredential,
  BlobSASPermissions,
  generateBlobSASQueryParameters,
} from "@azure/storage-blob";

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

    // Get Azure Storage configuration from environment variables
    const accountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
    const accountKey = process.env.AZURE_STORAGE_ACCOUNT_KEY;
    const containerName = process.env.AZURE_STORAGE_CONTAINER_NAME || "audio-files";

    if (!accountName || !accountKey) {
      console.error("Azure Storage credentials not configured");
      return NextResponse.json(
        { error: "Storage service not configured" },
        { status: 500 }
      );
    }

    // Create BlobServiceClient with shared key credentials
    const sharedKeyCredential = new StorageSharedKeyCredential(
      accountName,
      accountKey
    );
    const blobServiceClient = new BlobServiceClient(
      `https://${accountName}.blob.core.windows.net`,
      sharedKeyCredential
    );

    const containerClient = blobServiceClient.getContainerClient(containerName);

    // Generate SAS URLs for each file
    const uploads: UploadInfo[] = body.files.map((file) => {
      const timestamp = Date.now();
      const randomString = Math.random().toString(36).substring(2, 8);
      const sanitizedFileName = file.name.replace(/[^a-zA-Z0-9._-]/g, "_");
      const blobName = `${timestamp}-${randomString}-${sanitizedFileName}`;

      const blobClient = containerClient.getBlobClient(blobName);

      // Set SAS token expiration to 1 hour from now
      const expiresOn = new Date(Date.now() + 60 * 60 * 1000);

      // Generate SAS token with write permissions
      const sasToken = generateBlobSASQueryParameters(
        {
          containerName,
          blobName,
          permissions: BlobSASPermissions.parse("cw"), // create and write permissions
          expiresOn,
        },
        sharedKeyCredential
      ).toString();

      const blobUrl = blobClient.url;
      const uploadUrl = `${blobUrl}?${sasToken}`;

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
