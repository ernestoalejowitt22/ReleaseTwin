import type { Metadata } from "next";
import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import { SITE_DESCRIPTION } from "@/lib/site";
import { FlaskConical, ShieldCheck, EyeOff, ServerCog } from "lucide-react";
import { Button } from "@/components/ui/button";
import { HOMEPAGE_FEATURES, FEATURE_COPY } from "@/lib/plans";

const TRUST = [
  {
    icon: ServerCog,
    title: "Execution stays in your infra",
    description:
      "The CLI runs on your machine or in your CI. The hosted platform is a control plane — accounts, tokens, dashboard — never a test runner.",
  },
  {
    icon: EyeOff,
    title: "Only metadata leaves, by default",
    description:
      "Case ID, oracle reference, fixture hash, pass/fail, classification. Never fixture content, response bodies, or credentials — the ingest contract has no field for them. We're independent and self-funded, so our incentive is your renewal, not your data.",
  },
  {
    icon: ShieldCheck,
    title: "Redaction runs in your CLI",
    description:
      "Evidence upload is opt-in and per-project. Auth headers, credential-shaped fields, and resolved secrets are stripped before the socket opens — in code you can read.",
  },
];

/**
 * landing-demo-ci-loop: the demo is one real loop — a pull request fails the ReleaseTwin
 * gate, a fix turns it green, the evidence lands on the dashboard. The PR panels are
 * regenerated from the actual run summaries of NAHA PR #74 by
 * `web/scripts/capture-landing-demo.mjs` (SVG, committed under public/demo/), so they
 * can't drift from the real `integrations/github-action` renderer. The dashboard panels
 * are real screenshots of the hosted dashboard — see docs/landing-demo.md.
 */
const CI_LOOP_PANELS = [
  {
    img: "/demo/pr-check-failed.svg",
    w: 560,
    h: 56,
    alt: "A failing ReleaseTwin check on a pull request: 1 passed, 1 failed",
    claim: "It's a real merge gate — a failing check you make required, the same as unit tests.",
  },
  {
    img: "/demo/pr-comment-failed.svg",
    w: 780,
    h: 298,
    alt: "The ReleaseTwin pull-request comment: failed, 1 passed · 1 failed, with a table row for the failing case DEMO-GATE-1",
    claim: "The verdict is readable — totals, flag-proof, and the notable cases, updated in place on every run.",
  },
  {
    img: "/demo/pr-check-passed.svg",
    w: 560,
    h: 56,
    alt: "A passing ReleaseTwin check on a pull request after the fix",
    claim: "Push the fix and the same check goes green — the comment updates in place, no new noise.",
  },
] as const;

export const metadata: Metadata = {
  // Home page owns the full <title> rather than the "%s — ReleaseTwin" template.
  title: { absolute: "ReleaseTwin — self-serve release-proof testing" },
  description: SITE_DESCRIPTION,
  alternates: { canonical: "/" },
  openGraph: {
    title: "ReleaseTwin — self-serve release-proof testing",
    description: SITE_DESCRIPTION,
    url: "/",
  },
};

export default async function LandingPage() {
  const { userId } = await auth();

  return (
    <main className="flex w-full flex-1 flex-col items-center">
      {/* Hero */}
      <section className="mx-auto flex w-full max-w-5xl flex-col items-center gap-6 px-6 py-20 text-center">
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">ReleaseTwin</h1>
        <p className="max-w-xl text-lg text-muted-foreground">
          Self-serve release-proof testing. Sign in to get an API token and see
          your uploaded run history.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-3">
          <Button asChild size="lg">
            {userId ? (
              <Link href="/dashboard">Go to dashboard</Link>
            ) : (
              <Link href="/sign-in">Sign in to get started</Link>
            )}
          </Button>
          <Button asChild size="lg" variant="outline">
            <Link href="/docs/quickstart">Read the quickstart</Link>
          </Button>
        </div>

        <div className="mt-6 w-full max-w-2xl">
          {/* <object>, not <img>: svg-term's CSS keyframe animation only runs when the SVG is its
              own document. The nested <img> is the static fallback. */}
          <object
            type="image/svg+xml"
            data="/demo-flag-proof.svg"
            aria-label="Terminal recording: running a zero-credential HTTP case, then a flag-proof run that reports FLAGPROOF CHECKOUT-FIX-1 (Passed)"
            className="w-full rounded-xl ring-1 ring-foreground/10"
            style={{ aspectRatio: "828 / 435" }}
          >
            {/* eslint-disable-next-line @next/next/no-img-element -- animated asciinema SVG */}
            <img
              src="/demo-flag-proof.svg"
              alt="Terminal recording of a passing flag-proof run"
              className="w-full rounded-xl ring-1 ring-foreground/10"
            />
          </object>
        </div>
      </section>

      {/* The CI loop — merge gate → readable verdict → dashboard */}
      <section className="mx-auto flex w-full max-w-3xl flex-col items-center gap-3 px-6 pb-20">
        <h2 className="text-2xl font-semibold tracking-tight">One loop, on every pull request</h2>
        <p className="mb-6 max-w-xl text-center text-sm text-muted-foreground">
          A change opens a PR. ReleaseTwin runs your cases in your own runner and reports
          back. Here it catches a regression, blocks the merge, then goes green on the fix —
          real output from a real run.
        </p>
        <ol className="flex w-full flex-col gap-10">
          {CI_LOOP_PANELS.map((panel) => (
            <li key={panel.img} className="flex flex-col items-center gap-3">
              {/* Local trusted SVGs generated by web/scripts/capture-landing-demo.mjs;
                  plain <img> to avoid next/image's SVG gate, matching the hero recording. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={panel.img}
                width={panel.w}
                height={panel.h}
                alt={panel.alt}
                loading="lazy"
                className="w-full max-w-2xl rounded-lg ring-1 ring-foreground/10"
              />
              <p className="max-w-xl text-center text-sm text-muted-foreground">{panel.claim}</p>
            </li>
          ))}
        </ol>
        <p className="mt-8 max-w-xl text-center text-sm text-muted-foreground">
          Connect the hosted dashboard and each run&apos;s history and{" "}
          <Link href="/docs/hosted-platform" className="underline underline-offset-4 hover:text-foreground">
            redacted evidence
          </Link>{" "}
          lands there too — only metadata leaves your infra.
        </p>
      </section>

      {/* Trust — your data stays put */}
      <section className="w-full border-y bg-muted/30">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-6 py-16">
          <div className="flex flex-col gap-2 text-center">
            <h2 className="text-2xl font-semibold tracking-tight">Your data stays put</h2>
            <p className="mx-auto max-w-2xl text-sm text-muted-foreground">
              Built for integration-heavy teams who can&apos;t send their test data to a
              vendor. Nothing here asks you to.
            </p>
          </div>
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
            {TRUST.map((item) => (
              <div key={item.title} className="flex flex-col gap-2">
                <item.icon className="size-5 text-primary" />
                <p className="text-sm font-semibold">{item.title}</p>
                <p className="text-sm text-muted-foreground">{item.description}</p>
              </div>
            ))}
          </div>
          <p className="text-center text-xs text-muted-foreground">
            <Link href="/docs" className="underline underline-offset-4 hover:text-foreground">
              How it works
            </Link>
          </p>
        </div>
      </section>

      {/* Feature grid — what the hosted dashboard adds, sourced from the plan catalog */}
      <section className="mx-auto w-full max-w-5xl px-6 py-16">
        <div className="mb-8 flex flex-col gap-2 text-center">
          <h2 className="text-2xl font-semibold tracking-tight">What the hosted dashboard adds</h2>
          <p className="mx-auto max-w-2xl text-sm text-muted-foreground">
            On top of the open-source engine. See the{" "}
            <Link href="/features" className="underline underline-offset-4 hover:text-foreground">
              full features list
            </Link>{" "}
            for every capability and its tier.
          </p>
        </div>
        <div className="grid grid-cols-1 gap-6 text-left sm:grid-cols-2">
          {HOMEPAGE_FEATURES.map((key) => {
            const copy = FEATURE_COPY[key];
            return (
              <div key={key} className="flex gap-3">
                <FlaskConical className="mt-0.5 size-5 shrink-0 text-primary" />
                <div>
                  <p className="text-sm font-semibold">{copy.label}</p>
                  <p className="text-sm text-muted-foreground">{copy.description}</p>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* Closing CTA */}
      <section className="mx-auto flex w-full max-w-5xl flex-col items-center gap-5 px-6 pb-24 text-center">
        <h2 className="text-2xl font-semibold tracking-tight">Try it against your own API</h2>
        <p className="max-w-xl text-sm text-muted-foreground">
          The HTTP example needs no credentials. Run it, then point a case at your own
          endpoint. Connect the dashboard when you want run history.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-3">
          <Button asChild size="lg">
            <Link href="/docs/quickstart">Quickstart</Link>
          </Button>
          <Button asChild size="lg" variant="outline">
            <Link href="/pricing">See pricing</Link>
          </Button>
        </div>
        <p className="text-xs text-muted-foreground">
          We&apos;re working hands-on with a small number of design partners. Founding customers
          lock their pricing and get direct access to the person building it.
        </p>
      </section>
    </main>
  );
}
