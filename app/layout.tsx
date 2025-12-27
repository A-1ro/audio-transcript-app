import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Batch Speech Transcription",
  description: "Audio file batch transcription application",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ja">
      <body>{children}</body>
    </html>
  );
}
