import { NextResponse } from "next/server";

export async function GET() {
  try {
    // Get Azure Functions API URL from environment variable
    const functionsUrl = process.env.AZURE_FUNCTIONS_URL;

    if (!functionsUrl) {
      // If no Azure Functions URL is configured, return error
      // In production, this should be configured in environment variables
      console.error("AZURE_FUNCTIONS_URL is not configured");
      return NextResponse.json(
        { error: "Server configuration error" },
        { status: 500 }
      );
    }

    // Call the Azure Function to get jobs
    const response = await fetch(`${functionsUrl}/api/jobs`, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch jobs: ${response.statusText}`);
    }

    const jobs = await response.json();
    return NextResponse.json(jobs);
  } catch (error) {
    console.error("Error fetching jobs:", error);
    return NextResponse.json(
      { error: "Failed to fetch jobs" },
      { status: 500 }
    );
  }
}
