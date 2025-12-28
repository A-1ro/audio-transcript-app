import Link from "next/link";

export default function Home() {
  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="bg-soft-surface p-12 rounded-2xl shadow-soft-xl max-w-2xl w-full text-center border border-soft-border">
        <h1 className="text-4xl font-bold mb-4 text-soft-text tracking-tight">Batch Speech Transcription</h1>
        <p className="text-soft-subtext mb-10 text-lg">
          Simple, fast, and accurate audio transcription for your workflow.
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <Link
            href="/upload"
            className="inline-flex items-center justify-center bg-soft-primary hover:bg-soft-primaryHover text-white font-medium py-3 px-8 rounded-xl shadow-soft transition-all transform hover:-translate-y-0.5"
          >
            Start Uploading
          </Link>
          <Link
            href="/jobs"
            className="inline-flex items-center justify-center bg-white border border-soft-border hover:bg-gray-50 text-soft-text font-medium py-3 px-8 rounded-xl shadow-soft-sm transition-all"
          >
            View Jobs
          </Link>
        </div>
      </div>
    </div>
  );
}
