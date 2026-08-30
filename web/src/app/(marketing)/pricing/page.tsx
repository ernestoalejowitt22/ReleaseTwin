import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = {
  title: "Pricing — ReleaseTwin",
  description:
    "The CLI and adapters are free and source-available. The hosted dashboard is priced per project — free to start, from ~$49/project/mo for Team.",
};

const CONTACT = "mailto:ernestoalejo22@gmail.com?subject=ReleaseTwin%20early%20access";

type Row = { label: string; free: string | boolean; team: string | boolean; enterprise: string | boolean };

const ROWS: Row[] = [
  { label: "CLI, Core & adapters (run anywhere)", free: true, team: true, enterprise: true },
  { label: "Flag-proof runs", free: true, team: true, enterprise: true },
  { label: "Projects", free: "1", team: "Unlimited (billed per active)", enterprise: "Unlimited" },
  { label: "Uploaded run history", free: true, team: true, enterprise: true },
  { label: "Evidence viewer (request/response + screenshots)", free: false, team: true, enterprise: true },
  { label: "Evidence retention", free: "30 days", team: "12 months", enterprise: "Custom" },
  { label: "Custom redaction allow/deny rules", free: false, team: true, enterprise: true },
  { label: "SSO, audit log, private deployment", free: false, team: false, enterprise: true },
  { label: "Founding Setup onboarding", free: false, team: false, enterprise: "Included" },
  { label: "Support", free: "Community / GitHub", team: "Email", enterprise: "SLA + shared Slack" },
];

function Cell({ value }: { value: string | boolean }) {
  if (value === true) return <Check className="mx-auto size-4 text-primary" />;
  if (value === false) return <Minus className="mx-auto size-4 text-muted-foreground/50" />;
  return <span className="text-sm">{value}</span>;
}

const PLANS = [
  {
    name: "Free",
    price: "$0",
    unit: "1 project",
    blurb: "Run cases and land results on a dashboard.",
    features: [
      "CLI, execution kernel & all adapters",
      "Flag-proof runs, uploaded run history",
      "1 project · 30-day evidence retention",
      "Community support",
    ],
    cta: { label: "Create an account", href: "/sign-in" as const, variant: "default" as const },
    highlight: false,
  },
  {
    name: "Team",
    price: "~$49",
    unit: "per project / month",
    blurb: "Unlimited projects and the full evidence trail.",
    features: [
      "Everything in Free, plus:",
      "Unlimited projects (billed per active)",
      "Evidence viewer — request/response + screenshots",
      "12-month retention · custom redaction rules",
      "Email support",
    ],
    cta: { label: "Request early access", href: CONTACT, variant: "outline" as const, external: true },
    highlight: false,
    note: "Billed annually (~$59 on-demand). Early-access placeholder — final number set from the first cohort's usage.",
  },
  {
    name: "Enterprise",
    price: "~$99",
    unit: "per project / month",
    blurb: "Controls, private deployment, and hands-on onboarding.",
    features: [
      "Everything in Team, plus:",
      "SSO, audit log, private deployment",
      "Custom retention",
      "Founding Setup — your first workflow wired, included",
      "SLA + shared Slack",
    ],
    cta: { label: "Talk to us", href: CONTACT, variant: "outline" as const, external: true },
    highlight: false,
    note: "Billed annually.",
  },
];

export default function PricingPage() {
  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-12 px-6 py-16">
      <header className="flex flex-col items-center gap-3 text-center">
        <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">Pricing</h1>
        <p className="max-w-2xl text-muted-foreground">
          The CLI, execution kernel, and adapters are free and source-available — they run
          entirely in your own infrastructure. The hosted dashboard adds run history and
          evidence on top, priced per project so it scales with how much of your release
          surface you actually prove.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {PLANS.map((plan) => (
          <Card
            key={plan.name}
            className={`flex flex-col ${plan.highlight ? "ring-2 ring-primary" : ""}`}
          >
            <CardHeader className="gap-2">
              <div className="flex items-center justify-between">
                <CardTitle>{plan.name}</CardTitle>
                {plan.highlight ? <Badge>Most popular</Badge> : null}
              </div>
              <div className="flex items-baseline gap-1.5">
                <p className="text-2xl font-semibold">{plan.price}</p>
                <span className="text-xs text-muted-foreground">{plan.unit}</span>
              </div>
              <p className="text-sm text-muted-foreground">{plan.blurb}</p>
            </CardHeader>
            <CardContent className="flex flex-1 flex-col gap-4">
              <ul className="flex flex-col gap-2 text-sm">
                {plan.features.map((f) => (
                  <li key={f} className="flex gap-2">
                    <Check className="mt-0.5 size-4 shrink-0 text-primary" />
                    <span className={f.endsWith("plus:") ? "text-muted-foreground" : undefined}>{f}</span>
                  </li>
                ))}
              </ul>
              <div className="mt-auto flex flex-col gap-3">
                <Button asChild variant={plan.cta.variant}>
                  {"external" in plan.cta && plan.cta.external ? (
                    <a href={plan.cta.href}>{plan.cta.label}</a>
                  ) : (
                    <Link href={plan.cta.href as "/sign-in"}>{plan.cta.label}</Link>
                  )}
                </Button>
                {plan.note ? (
                  <p className="text-xs text-muted-foreground">{plan.note}</p>
                ) : null}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-xl font-semibold tracking-tight">What&apos;s included</h2>
        <div className="overflow-x-auto rounded-xl border">
          <table className="w-full text-left text-sm">
            <thead className="border-b bg-muted/50">
              <tr>
                <th className="px-4 py-3 font-medium">Feature</th>
                <th className="px-4 py-3 text-center font-medium">Free</th>
                <th className="px-4 py-3 text-center font-medium">Team</th>
                <th className="px-4 py-3 text-center font-medium">Enterprise</th>
              </tr>
            </thead>
            <tbody>
              {ROWS.map((row) => (
                <tr key={row.label} className="border-b last:border-0">
                  <td className="px-4 py-3">{row.label}</td>
                  <td className="px-4 py-3 text-center">
                    <Cell value={row.free} />
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Cell value={row.team} />
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Cell value={row.enterprise} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="flex flex-col gap-3 rounded-xl border bg-muted/30 p-6">
        <h2 className="text-lg font-semibold">Founding Setup</h2>
        <p className="max-w-2xl text-sm text-muted-foreground">
          Testing a non-REST system, a feature-flag source that isn&apos;t Azure DevOps, or a
          gnarly multi-step release check often needs scoped setup work. Founding Setup wires one
          critical workflow into your CI and hands you a written readout of what it catches —
          fixed scope, fee refunded if it isn&apos;t running against your real system at the end.
          Included with Enterprise; a paid add-on otherwise.
        </p>
        <div>
          <Button asChild variant="outline">
            <a href={CONTACT}>Get in touch</a>
          </Button>
        </div>
      </section>

      <section className="flex flex-col gap-3 rounded-xl border bg-muted/30 p-6">
        <h2 className="text-lg font-semibold">If we disappear, you keep working</h2>
        <p className="max-w-2xl text-sm text-muted-foreground">
          The CLI and execution kernel are open source (AGPL-3.0) and run entirely in your own
          infrastructure — a hosted outage never blocks a release. If we ever wind the company
          down, active hosted licenses convert to perpetual and the hosted source is published.
        </p>
        <div>
          <Button asChild variant="outline">
            <Link href="/docs/security">Read the continuity commitment</Link>
          </Button>
        </div>
      </section>

      <p className="text-center text-xs text-muted-foreground">
        ReleaseTwin is independent and self-funded. Prices are early-access placeholders and may
        change before general availability.
      </p>
    </main>
  );
}
