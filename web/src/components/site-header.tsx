import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import { FlaskConical } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ThemeToggle } from "@/components/theme-toggle";

const NAV = [
  { href: "/docs", label: "Docs" },
  { href: "/pricing", label: "Pricing" },
] as const;

const GITHUB_URL = "https://github.com/ernestoalejowitt22/ReleaseTwin";

/**
 * Shared chrome for the marketing surface (landing, pricing, docs). The dashboard renders its own
 * header inline and never mounts this one, so the two navigation contexts stay separate.
 */
export async function SiteHeader() {
  const { userId } = await auth();

  return (
    <header className="sticky top-0 z-40 w-full border-b bg-background/80 backdrop-blur">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-6">
        <Link href="/" className="flex items-center gap-2 font-semibold">
          <FlaskConical className="size-5 text-primary" />
          ReleaseTwin
        </Link>

        <nav className="flex items-center gap-1 text-sm">
          {NAV.map((item) => (
            <Button key={item.href} asChild variant="ghost" size="sm">
              <Link href={item.href}>{item.label}</Link>
            </Button>
          ))}
          <Button asChild variant="ghost" size="sm">
            <a href={GITHUB_URL} target="_blank" rel="noreferrer">
              GitHub
            </a>
          </Button>
          <ThemeToggle />
          <Button asChild size="sm">
            {userId ? (
              <Link href="/dashboard">Dashboard</Link>
            ) : (
              <Link href="/sign-in">Sign in</Link>
            )}
          </Button>
        </nav>
      </div>
    </header>
  );
}
