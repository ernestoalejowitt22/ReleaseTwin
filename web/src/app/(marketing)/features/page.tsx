import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus } from "lucide-react";
import { DocHeader } from "@/components/doc";
import { entitlementKeys, lowestTierWith, FEATURE_COPY } from "@/lib/plans";

export const metadata: Metadata = {
  title: "Features — ReleaseTwin",
  description:
    "Everything the open-source engine does in your own infrastructure, and everything the hosted dashboard adds — with the lowest tier that includes each capability.",
};

/**
 * Authored prose (proposal D1): a small, stable list of open-source-engine capabilities that are
 * always available with no account. These are not entitlements — they don't belong in the
 * tier-gated table — so hand-maintaining them next to their docs pages is clearer than a data file.
 */
const OSS_ENGINE: { label: string; description: string; docHref: string }[] = [
  {
    label: "HTTP + UI journey composition",
    description: "Chain HTTP and browser steps into one pinned case and run it from the CLI.",
    docHref: "/docs/case-files",
  },
  {
    label: "Flag-proof runs",
    description: "Run the same case known-bad and known-good to prove a fix actually changed the outcome.",
    docHref: "/docs/quickstart",
  },
  {
    label: "Config-driven adapters",
    description: "Select the HTTP, UI, and feature-flag adapters per project from releasetwin.yaml.",
    docHref: "/docs/case-files",
  },
  {
    label: "CLI-side redaction",
    description: "Auth headers, credential-shaped fields, and resolved ${ENV_VAR} secrets are stripped before anything leaves your machine.",
    docHref: "/docs/security",
  },
  {
    label: "Runs anywhere, no account",
    description: "The CLI and execution kernel are AGPL-3.0 and run entirely in your infrastructure or CI.",
    docHref: "/docs/quickstart",
  },
];

function Cell({ value }: { value: string | boolean }) {
  if (value === true) return <Check className="mx-auto size-4 text-primary" />;
  if (value === false) return <Minus className="mx-auto size-4 text-muted-foreground/50" />;
  return <span className="text-sm">{value}</span>;
}

export default function FeaturesPage() {
  const keys = entitlementKeys();

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-12 px-6 py-16">
      <DocHeader
        title="Features"
        lead="The open-source engine runs in your infrastructure with no account. The hosted dashboard adds run history and evidence on top — priced per project."
      />

      <section className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-xl font-semibold tracking-tight">Open-source engine</h2>
          <p className="text-sm text-muted-foreground">
            AGPL-3.0, runs anywhere, no account, free. Nothing here uploads your test data.
          </p>
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {OSS_ENGINE.map((item) => (
            <div key={item.label} className="flex flex-col gap-1 rounded-xl border p-4">
              <Link href={item.docHref} className="text-sm font-semibold underline-offset-4 hover:underline">
                {item.label}
              </Link>
              <p className="text-sm text-muted-foreground">{item.description}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-xl font-semibold tracking-tight">Hosted dashboard</h2>
          <p className="text-sm text-muted-foreground">
            Generated from the plan catalog the hosted API enforces. See{" "}
            <Link href="/pricing" className="underline underline-offset-4">pricing</Link> for the numbers.
          </p>
        </div>
        <div className="overflow-x-auto rounded-xl border">
          <table className="w-full text-left text-sm">
            <thead className="border-b bg-muted/50">
              <tr>
                <th className="px-4 py-3 font-medium">Capability</th>
                <th className="px-4 py-3 font-medium">What it does</th>
                <th className="px-4 py-3 font-medium">Included from</th>
              </tr>
            </thead>
            <tbody>
              {keys.map((key) => {
                const copy = FEATURE_COPY[key];
                const tier = lowestTierWith(key);
                return (
                  <tr key={key} className="border-b last:border-0 align-top">
                    <td className="px-4 py-3">
                      {copy.docHref ? (
                        <Link href={copy.docHref} className="font-medium underline-offset-4 hover:underline">
                          {copy.label}
                        </Link>
                      ) : (
                        <span className="font-medium">{copy.label}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{copy.description}</td>
                    <td className="px-4 py-3">{tier ? tier.name : <Cell value={false} />}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <p className="text-xs text-muted-foreground">
          &ldquo;Included from&rdquo; is the lowest tier whose entitlement set grants the capability;
          numeric limits (projects, retention) are shown per tier on the{" "}
          <Link href="/pricing" className="underline underline-offset-4">pricing comparison</Link>.
        </p>
      </section>
    </main>
  );
}
