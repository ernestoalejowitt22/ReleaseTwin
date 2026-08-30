import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = {
  title: "Docs — ReleaseTwin",
  description: "How ReleaseTwin works: the execution model, what leaves your network, and where to start.",
};

const NEXT = [
  {
    href: "/docs/quickstart" as const,
    title: "Quickstart",
    body: "Run the bundled HTTP example with no credentials, then point a case at your own API.",
  },
  {
    href: "/docs/case-files" as const,
    title: "Case files",
    body: "The YAML a case is made of — oracle, fixture, pipeline, cleanup, and flag-proof.",
  },
  {
    href: "/docs/hosted-platform" as const,
    title: "Hosted platform",
    body: "Issue a token, upload run history, and turn on the evidence viewer.",
  },
  {
    href: "/docs/security" as const,
    title: "Security & credentials",
    body: "Where secrets live, what crosses the network, and what the operator can see.",
  },
];

export default function DocsOverviewPage() {
  return (
    <>
      <DocHeader
        title="How ReleaseTwin works"
        lead="A local runner that proves a release candidate is safe — and a control plane that keeps the history. Execution never leaves your infrastructure."
      />

      <DocSection title="The two pieces">
        <P>
          <strong className="text-foreground">The CLI</strong> loads YAML case files, composes
          whichever adapters are configured, runs each case&apos;s pipeline, and exits non-zero on
          any failure — safe to wire straight into CI. It runs on your machine or your CI runner.
          Nothing else is required.
        </P>
        <P>
          <strong className="text-foreground">The hosted platform</strong> is an optional control
          plane: self-serve sign-up, projects and API tokens, and a dashboard of uploaded run
          history and flag-proof results. It is not a test runner — it never executes anything.
        </P>
      </DocSection>

      <DocSection title="What leaves your network">
        <P>By default, only report metadata is uploaded, and only if you set an API token:</P>
        <UL>
          <li>case ID, oracle reference, fixture hash, pass/fail, failure classification</li>
          <li>
            never fixture content, request/response bodies, or credentials — the ingest contract
            has no field for them
          </li>
        </UL>
        <P>
          You can opt in per project (paid tier) to also upload a{" "}
          <strong className="text-foreground">redacted evidence document</strong> — per-step
          request/response summaries, assertion detail, screenshots. Redaction runs in your own
          CLI, before anything is sent: auth headers, credential-shaped fields, and resolved
          secrets are stripped automatically, plus your own allow/deny rules.
        </P>
      </DocSection>

      <DocSection title="The core mechanics">
        <UL>
          <li>
            <strong className="text-foreground">Fixture integrity</strong> — every case names a
            fixture by SHA-256; a mismatch fails the case before the pipeline runs.
          </li>
          <li>
            <strong className="text-foreground">Prerequisite ownership</strong> — checks are
            three-state (satisfied / not satisfied / inconclusive) and each names an owner.
          </li>
          <li>
            <strong className="text-foreground">Cleanup</strong> — declared per case, runs even
            when the pipeline fails, no-ops safely when there is nothing to undo.
          </li>
          <li>
            <strong className="text-foreground">Failure classification</strong> — a failure is
            labelled (infrastructure, prerequisite, oracle, …), not just red.
          </li>
          <li>
            <strong className="text-foreground">Flag proof</strong> — run the same case
            known-bad and known-good and report a single discriminating outcome, so a broken
            build and a fixed one are actually distinguishable.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Next">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {NEXT.map((item) => (
            <Link key={item.href} href={item.href} className="group">
              <Card className="h-full transition-colors group-hover:bg-muted/50">
                <CardHeader>
                  <CardTitle className="text-base">{item.title}</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted-foreground">{item.body}</p>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      </DocSection>
    </>
  );
}
