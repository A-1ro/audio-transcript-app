import Link from "next/link";
import UploadForm from "./UploadForm";

export default function UploadPage() {
  return (
    <div className="min-h-screen bg-soft-bg py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto">
        <div className="mb-8 flex items-center justify-between">
          <Link
            href="/"
            className="text-soft-subtext hover:text-soft-primary transition-colors text-sm font-medium flex items-center gap-2"
          >
            <span>←</span> Back to Home
          </Link>
          <Link
            href="/jobs"
            className="text-soft-subtext hover:text-soft-primary transition-colors text-sm font-medium flex items-center gap-2"
          >
             View Jobs <span>→</span>
          </Link>
        </div>
        
        <div className="text-center mb-10">
          <h1 className="text-3xl font-bold text-soft-text mb-3 tracking-tight">
            New Transcription Job
          </h1>
          <p className="text-soft-subtext text-lg">
            Upload your audio files to start processing
          </p>
        </div>

        <div className="bg-soft-surface rounded-2xl shadow-soft-lg border border-soft-border p-6 sm:p-10">
          <UploadForm />
        </div>
      </div>
    </div>
  );
}
