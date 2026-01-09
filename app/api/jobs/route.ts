import { NextRequest, NextResponse } from "next/server";

export async function GET() {
  try {
    // Get Azure Functions API URL from environment variable
    const functionsUrl = process.env.AZURE_FUNCTIONS_URL;
    const apiKey = process.env.AZURE_FUNCTIONS_API_KEY;

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
    const headers: HeadersInit = {};
    if (apiKey) {
      headers["x-functions-key"] = apiKey;
    }

    const response = await fetch(`${functionsUrl}/api/jobs`, {
      method: "GET",
      headers,
    });

    if (!response.ok) {
      throw new Error("Failed to fetch jobs");
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

export async function POST(request: NextRequest) {
  try {
    // Get Azure Functions API URL from environment variable
    const functionsUrl = process.env.AZURE_FUNCTIONS_URL;
    const apiKey = process.env.AZURE_FUNCTIONS_API_KEY;

    if (!functionsUrl) {
      console.error("AZURE_FUNCTIONS_URL is not configured");
      return NextResponse.json(
        { error: "Server configuration error" },
        { status: 500 }
      );
    }

    // Parse request body with specific error handling
    let body;
    try {
      body = await request.json();
    } catch (error) {
      console.error("Invalid JSON in request body:", error);
      return NextResponse.json(
        { error: "Invalid JSON in request body" },
        { status: 400 }
      );
    }

    // Validate request body
    if (!body.audioFiles || !Array.isArray(body.audioFiles)) {
      return NextResponse.json(
        { error: "Invalid request: audioFiles array is required" },
        { status: 400 }
      );
    }

    if (body.audioFiles.length === 0) {
      return NextResponse.json(
        { error: "At least one audio file is required" },
        { status: 400 }
      );
    }

    // Call the Azure Function to create job
    const headers: HeadersInit = {
      "Content-Type": "application/json",
    };
    if (apiKey) {
      headers["x-functions-key"] = apiKey;
    }

    const response = await fetch(`${functionsUrl}/api/jobs`, {
      method: "POST",
      headers,
      body: JSON.stringify(body),
    });

    const responseData = await response.json();

    if (!response.ok) {
      console.error("Failed to create job:", responseData);
      return NextResponse.json(
        responseData,
        { status: response.status }
      );
    }

    return NextResponse.json(responseData, { status: 201 });
  } catch (error) {
    console.error("Error creating job:", error);
    return NextResponse.json(
      { error: "Failed to create job" },
      { status: 500 }
    );
  }
}
