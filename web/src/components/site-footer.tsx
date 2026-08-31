import Link from "next/link";

const GITHUB_URL = "https://github.com/ernestoalejowitt22/ReleaseTwin";

export function SiteFooter() {
  return (
    <footer className="mt-auto w-full border-t">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-2 px-6 py-8 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <p>
          ReleaseTwin — the CLI and adapters are source-available; execution and your
          data stay in your infrastructure.
        </p>
        <nav className="flex flex-wrap items-center gap-4">
          <Link href="/docs" className="hover:text-foreground">
            Docs
          </Link>
          <Link href="/features" className="hover:text-foreground">
            Features
          </Link>
          <Link href="/pricing" className="hover:text-foreground">
            Pricing
          </Link>
          <Link href="/terms" className="hover:text-foreground">
            Terms
          </Link>
          <Link href="/privacy" className="hover:text-foreground">
            Privacy
          </Link>
          <a href={GITHUB_URL} target="_blank" rel="noreferrer" className="hover:text-foreground">
            GitHub
          </a>
        </nav>
      </div>
    </footer>
  );
}
