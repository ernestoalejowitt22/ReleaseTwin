import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { CodeBlock } from "@/components/code-block";

export const metadata: Metadata = {
  title: "CI & GitHub Actions — ReleaseTwin",
  description: "Wire ReleaseTwin into a CI pipeline as a release-proof gate that blocks the merge on a real regression.",
};

export default function CiPage() {
  return (
    <>
      <DocHeader
        title="CI & GitHub Actions"
        lead="The CLI exits non-zero on any failure, so it drops into any pipeline as a required check."
      />

      <DocSection title="The pattern">
        <P>
          Run the CLI against your case directory. A non-zero exit fails the job, which fails the
          check, which blocks the merge — the same gate you already trust for unit tests, now
          covering release-critical integration behavior and flag proof.
        </P>
        <CodeBlock
          label=".github/workflows/release-proof.yml"
          code={`name: Release-proof gate

on:
  pull_request:

jobs:
  release-proof:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet build src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj -c Release --nologo
      - name: Run release-proof cases
        run: >
          dotnet run --project src/ReleaseTwin.Cli -c Release --no-build --
          cases/`}
        />
        <P>
          Once a versioned CLI image is published, the build step goes away and the gate is a
          single line:
        </P>
        <CodeBlock
          code={`- run: docker run --rm -v "$PWD/cases:/workspace:ro" ghcr.io/OWNER/releasetwin/cli:VERSION`}
        />
      </DocSection>

      <DocSection title="Credentials">
        <UL>
          <li>
            HTTP-only cases need nothing. Case files reference{" "}
            <code className="text-foreground">${"{ENV_VAR}"}</code>; set those from your CI&apos;s
            secret store.
          </li>
          <li>
            A flag-proof leg needs its flag source&apos;s credentials as job env — e.g.{" "}
            <code className="text-foreground">
              LAUNCHDARKLY_API_TOKEN: {"${{ secrets.LAUNCHDARKLY_API_TOKEN }}"}
            </code>
            .
          </li>
          <li>
            If you connect the hosted dashboard, add{" "}
            <code className="text-foreground">RELEASETWIN_API_TOKEN</code> and{" "}
            <code className="text-foreground">RELEASETWIN_API_URL</code> — run history and evidence
            then land on the dashboard for every CI run.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Live example in this repo">
        <P>
          <code className="text-foreground">.github/workflows/releasetwin-demo.yml</code> runs the
          zero-credential HTTP case on every PR and on demand — a real, green release-proof gate you
          can copy. A passing run&apos;s step output:
        </P>
        <CodeBlock
          label="Run release-proof cases"
          code={`$ dotnet run --project src/ReleaseTwin.Cli -c Release --no-build -- demo/quickstart/cases

PASS HTTP-DEMO-1
1 passed, 0 failed`}
        />
      </DocSection>

      <DocSection title="Next">
        <P>
          <Link href="/docs/case-files" className="text-primary underline underline-offset-4">
            Case files
          </Link>{" "}
          for what each case can assert, or{" "}
          <Link href="/docs/hosted-platform" className="text-primary underline underline-offset-4">
            Hosted platform
          </Link>{" "}
          to send CI run history to the dashboard.
        </P>
      </DocSection>
    </>
  );
}
