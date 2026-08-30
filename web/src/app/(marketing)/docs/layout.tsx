import type { ReactNode } from "react";
import Link from "next/link";

const SECTIONS = [
  { href: "/docs", label: "Overview" },
  { href: "/docs/quickstart", label: "Quickstart" },
  { href: "/docs/case-files", label: "Case files" },
  { href: "/docs/hosted-platform", label: "Hosted platform" },
  { href: "/docs/security", label: "Security & credentials" },
] as const;

export default function DocsLayout({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-1 gap-10 px-6 py-12">
      <aside className="hidden w-48 shrink-0 md:block">
        <nav className="sticky top-20 flex flex-col gap-1 text-sm">
          <p className="px-2 pb-1 text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Documentation
          </p>
          {SECTIONS.map((section) => (
            <Link
              key={section.href}
              href={section.href}
              className="rounded-md px-2 py-1.5 text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              {section.label}
            </Link>
          ))}
        </nav>
      </aside>
      <article className="min-w-0 flex-1">{children}</article>
    </div>
  );
}
