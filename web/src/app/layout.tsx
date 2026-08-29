import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { ClerkProvider } from "@clerk/nextjs";
import { Toaster } from "@/components/ui/sonner";
import { ThemeProvider } from "@/components/theme-provider";
import "./globals.css";

// dashboard-visual-refresh: no anti-flash-of-wrong-theme script here — every form of <script>
// element tried (a plain JSX <script>, next-themes' own internal one, and even Next's own official
// next/script with strategy="beforeInteractive") reproducibly blanks the entire page under this
// project's exact Next.js 16 / React 19 combination ("Encountered a script tag while rendering
// React component", confirmed against a real `next dev` server with a fresh .next cache, not an
// HMR artifact). Accepted trade-off, documented rather than silently dropped: a cold load can
// briefly flash the light theme before ThemeProvider's client-side read applies the real one. See
// theme-provider.tsx for the full writeup and design.md's Risks section.

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "ReleaseTwin",
  description: "Self-serve release-proof testing.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <ClerkProvider>
      <html
        lang="en"
        suppressHydrationWarning
        className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
      >
        <body className="min-h-full flex flex-col">
          <ThemeProvider>
            {children}
            <Toaster />
          </ThemeProvider>
        </body>
      </html>
    </ClerkProvider>
  );
}
