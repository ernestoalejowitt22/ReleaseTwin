import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { CodeBlock } from "@/components/code-block";

export const metadata: Metadata = {
  title: "Hosted platform — ReleaseTwin",
  description: "Issue an API token, upload run history, and turn on the redacted evidence viewer.",
};

export default function HostedPlatformPage() {
  return (
    <>
      <DocHeader
        title="Hosted platform"
        lead="An optional control plane for run history and evidence. It never executes your cases — the CLI still runs entirely in your infrastructure."
      />

      <DocSection title="1. Create a project and token">
        <UL>
          <li>
            <Link href="/sign-in" className="text-primary underline underline-offset-4">
              Sign in
            </Link>{" "}
            — self-serve, no approval step.
          </li>
          <li>Create a project on the dashboard.</li>
          <li>Issue an API token for it. You&apos;ll see the token once — store it as a secret.</li>
        </UL>
      </DocSection>

      <DocSection title="2. Point the CLI at it">
        <CodeBlock
          code={`export RELEASETWIN_API_TOKEN=<token from the dashboard>
export RELEASETWIN_API_URL=<your hosted API URL>

dotnet run --project src/ReleaseTwin.Cli -- examples/cases`}
        />
        <P>
          Uploads happen automatically after each case. A failed upload prints a warning but
          never changes a case&apos;s pass/fail result or the CLI&apos;s exit code.
        </P>
      </DocSection>

      <DocSection title="3. What gets uploaded">
        <P>By default, metadata only:</P>
        <UL>
          <li>case ID, oracle reference, fixture hash, pass/fail, failure classification</li>
          <li>flag-proof outcome and build identity</li>
          <li>
            never fixture content, request/response bodies, or credentials — there is no field
            for them in the ingest contract
          </li>
        </UL>
      </DocSection>

      <DocSection title="4. Evidence viewer (opt-in, paid)">
        <P>
          Turn on evidence capture per project to also upload a redacted evidence document —
          per-step request/response summaries, assertion path / expected / observed, and UI
          screenshots. It renders as a per-report drill-down on the dashboard.
        </P>
        <UL>
          <li>
            Redaction runs in <strong className="text-foreground">your</strong> CLI, before
            upload: auth headers, credential-shaped fields, and resolved{" "}
            <code className="text-foreground">${"{ENV_VAR}"}</code> secrets are stripped
            automatically.
          </li>
          <li>
            You add your own allow/deny rules; the redactor fails closed on any rule it
            can&apos;t evaluate.
          </li>
          <li>
            Retention is a per-project window (default 30 days, max 365). A daily purge deletes
            expired evidence and leaves the metadata report intact.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Plans">
        <P>
          The Free tier includes one project, run history, and 30-day retention. The evidence
          viewer, unlimited projects, and longer retention are on paid tiers — see{" "}
          <Link href="/pricing" className="text-primary underline underline-offset-4">
            pricing
          </Link>
          .
        </P>
      </DocSection>
    </>
  );
}
