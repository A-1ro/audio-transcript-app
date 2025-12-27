import { NextResponse } from "next/server";

// Mock job data - in production this would query Azure Cosmos DB
const mockJobs = [
  {
    jobId: "550e8400-e29b-41d4-a716-446655440001",
    status: "Completed",
    createdAt: "2025-12-27T10:21:00Z",
  },
  {
    jobId: "550e8400-e29b-41d4-a716-446655440002",
    status: "Processing",
    createdAt: "2025-12-27T10:45:00Z",
  },
  {
    jobId: "550e8400-e29b-41d4-a716-446655440003",
    status: "Uploaded",
    createdAt: "2025-12-27T11:30:00Z",
  },
];

export async function GET() {
  // Simulate Azure Cosmos DB response
  // In production: query Cosmos DB and return actual job records
  return NextResponse.json(mockJobs);
}
