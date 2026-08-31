import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  annualSavingsPct,
  defaultPrice,
  entitlementKeys,
  formatEntitlementValue,
  formatPrice,
  priceFor,
  tiersForDisplay,
  FEATURE_COPY,
  type PlanTier,
} from "@/lib/plans";

export const metadata: Metadata = {
  title: "Pricing — ReleaseTwin",
  description:
    "The CLI and adapters are free and source-available. The hosted dashboard is priced per project — free to start, from ~$49/project/mo for Team.",
};

const CONTACT = "mailto:ernestoalejo22@gmail.com?subject=ReleaseTwin%20early%20access";

/**
 * Authored, per-tier framing only — the tier set, prices, support, and feature values all come
 * from the plan catalog (`@/lib/plans`). Adding or re-pricing a tier in `hosted/plans.json` is
 * reflected here with no edit to this file.
 */
const TIER_META: Record<
  PlanTier["id"],
  { blurb: string; cta: { label: string; href: string; external?: boolean; variant: "default" | "outline" }; note?: string }
> = {
  free: {
    blurb: "Run cases and land results on a dashboard.",
    cta: { label: "Create an account", href: "/sign-in", variant: "default" },
  },
  team: {
    blurb: "Unlimited projects and the full evidence trail.",
    cta: { label: "Request early access", href: CONTACT, external: true, variant: "outline" },
    note: "Monthly or annual, per project. Cancel anytime.",
  },
  enterprise: {
    blurb: "Controls, private deployment, and hands-on onboarding.",
    cta: { label: "Talk to us", href: CONTACT, external: true, variant: "outline" },
    note: "Billed annually. Founding Setup onboarding included.",
  },
};

const PLACEHOLDER_CAVEAT =
  "Early-access placeholder — the final number is set from the first cohort's usage.";

function Cell({ value }: { value: string | boolean }) {
  if (value === true) return <Check className="mx-auto size-4 text-primary" />;
  if (value === false) return <Minus className="mx-auto size-4 text-muted-foreground/50" />;
  return <span className="text-sm">{value}</span>;
}

export default function PricingPage() {
  const tiers = tiersForDisplay();
  const keys = entitlementKeys();

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
        {tiers.map((tier) => {
          const meta = TIER_META[tier.id];
          const price = defaultPrice(tier);
          const annual = priceFor(tier, "annual");
          const savings = annualSavingsPct(tier);
          return (
            <Card key={tier.id} className="flex flex-col">
              <CardHeader className="gap-2">
                <CardTitle>{tier.name}</CardTitle>
                <div className="flex items-baseline gap-1.5">
                  <p className="text-2xl font-semibold">{formatPrice(price)}</p>
                  <span className="text-xs text-muted-foreground">{price.unit}</span>
                </div>
                {annual && annual.interval !== price.interval ? (
                  <p className="text-xs text-muted-foreground">
                    or {formatPrice(annual)}/{annual.unit} billed annually
                    {savings ? ` — save ${savings}%` : ""}
                  </p>
                ) : null}
                <p className="text-sm text-muted-foreground">{meta.blurb}</p>
              </CardHeader>
              <CardContent className="flex flex-1 flex-col gap-4">
                <p className="text-sm text-muted-foreground">
                  Support: <span className="text-foreground">{tier.support}</span>
                </p>
                <div className="mt-auto flex flex-col gap-3">
                  <Button asChild variant={meta.cta.variant}>
                    {meta.cta.external ? (
                      <a href={meta.cta.href}>{meta.cta.label}</a>
                    ) : (
                      <Link href={meta.cta.href as "/sign-in"}>{meta.cta.label}</Link>
                    )}
                  </Button>
                  {price.placeholder ? (
                    <p className="text-xs text-muted-foreground">{PLACEHOLDER_CAVEAT}</p>
                  ) : null}
                  {meta.note ? <p className="text-xs text-muted-foreground">{meta.note}</p> : null}
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-xl font-semibold tracking-tight">What&apos;s included</h2>
        <div className="overflow-x-auto rounded-xl border">
          <table className="w-full text-left text-sm">
            <thead className="border-b bg-muted/50">
              <tr>
                <th className="px-4 py-3 font-medium">Feature</th>
                {tiers.map((tier) => (
                  <th key={tier.id} className="px-4 py-3 text-center font-medium">
                    {tier.name}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              <tr className="border-b">
                <td className="px-4 py-3">CLI, execution kernel &amp; adapters (run anywhere)</td>
                {tiers.map((tier) => (
                  <td key={tier.id} className="px-4 py-3 text-center">
                    <Cell value={true} />
                  </td>
                ))}
              </tr>
              <tr className="border-b">
                <td className="px-4 py-3">Uploaded run history</td>
                {tiers.map((tier) => (
                  <td key={tier.id} className="px-4 py-3 text-center">
                    <Cell value={true} />
                  </td>
                ))}
              </tr>
              {keys.map((key) => (
                <tr key={key} className="border-b last:border-0">
                  <td className="px-4 py-3">{FEATURE_COPY[key].label}</td>
                  {tiers.map((tier) => (
                    <td key={tier.id} className="px-4 py-3 text-center">
                      <Cell value={formatEntitlementValue(key, tier.entitlements[key])} />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="text-xs text-muted-foreground">
          Every row is generated from the plan catalog the hosted API enforces — see the full
          list on the <Link href="/features" className="underline underline-offset-4">features page</Link>.
        </p>
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
