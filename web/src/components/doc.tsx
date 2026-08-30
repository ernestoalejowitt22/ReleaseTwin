import type { ReactNode } from "react";

/** Minimal doc-page primitives — the project doesn't pull in a typography plugin. */

export function DocHeader({ title, lead }: { title: string; lead?: string }) {
  return (
    <header className="mb-8 flex flex-col gap-3 border-b pb-6">
      <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
      {lead ? <p className="text-lg text-muted-foreground">{lead}</p> : null}
    </header>
  );
}

export function DocSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mb-10 flex flex-col gap-4">
      <h2 className="text-xl font-semibold tracking-tight">{title}</h2>
      {children}
    </section>
  );
}

export function P({ children }: { children: ReactNode }) {
  return <p className="text-sm leading-relaxed text-muted-foreground">{children}</p>;
}

export function UL({ children }: { children: ReactNode }) {
  return (
    <ul className="ml-5 list-disc space-y-1.5 text-sm leading-relaxed text-muted-foreground marker:text-muted-foreground/50">
      {children}
    </ul>
  );
}
