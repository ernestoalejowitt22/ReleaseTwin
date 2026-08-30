import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import {
  GitBranch,
  KeyRound,
  Workflow,
  FlaskConical,
  ShieldCheck,
  EyeOff,
  ServerCog,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const FEATURES = [
  {
    icon: Workflow,
    title: "Compose journeys visually",
    description: "Chain HTTP and UI steps into a real pipeline, then pin and run it from the CLI.",
  },
  {
    icon: KeyRound,
    title: "Hosted secrets, not env vars",
    description: "Store credentials once per project — the CLI fetches them wherever it runs.",
  },
  {
    icon: FlaskConical,
    title: "Flag-proof",
    description: "Prove a fix actually works by running the same case known-bad and known-good.",
  },
  {
    icon: GitBranch,
    title: "Self-serve, real CLI in minutes",
    description: "Sign up, issue a token, run a zero-credential example case — no setup call.",
  },
];

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
      "Case ID, oracle reference, fixture hash, pass/fail, classification. Never fixture content, response bodies, or credentials — the ingest contract has no field for them.",
  },
  {
    icon: ShieldCheck,
    title: "Redaction runs in your CLI",
    description:
      "Evidence upload is opt-in and per-project. Auth headers, credential-shaped fields, and resolved secrets are stripped before the socket opens — in code you can read.",
  },
];

/**
 * dashboard-visual-refresh design.md called for "a real product screenshot" — this is a live
 * render of the actual Card/Table/Badge components instead of a static image, so it can never go
 * visually stale relative to the real dashboard theme. Case IDs are the same ones the product's own
 * bundled zero-credential examples already produce (see examples/cases/), not invented data.
 */
function DashboardPreview() {
  return (
    <Card className="w-full max-w-xl text-left shadow-lg">
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Run history</CardTitle>
        <Badge>Paid plan</Badge>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Case</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead>Uploaded</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell>HTTP-DEMO-1</TableCell>
              <TableCell>
                <Badge variant="default">PASS</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
            <TableRow>
              <TableCell>AUTH-CHAIN-DEMO-1</TableCell>
              <TableCell>
                <Badge variant="default">PASS</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
            <TableRow>
              <TableCell>CLM-042</TableCell>
              <TableCell>
                <Badge variant="destructive">FAIL</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

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

      {/* Dashboard preview */}
      <section className="flex w-full flex-col items-center px-6 pb-20">
        <DashboardPreview />
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

      {/* Feature grid */}
      <section className="mx-auto w-full max-w-5xl px-6 py-16">
        <div className="grid grid-cols-1 gap-6 text-left sm:grid-cols-2">
          {FEATURES.map((feature) => (
            <div key={feature.title} className="flex gap-3">
              <feature.icon className="mt-0.5 size-5 shrink-0 text-primary" />
              <div>
                <p className="text-sm font-semibold">{feature.title}</p>
                <p className="text-sm text-muted-foreground">{feature.description}</p>
              </div>
            </div>
          ))}
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
          We&apos;re working hands-on with a small number of design partners — free, in
          exchange for feedback.
        </p>
      </section>
    </main>
  );
}
