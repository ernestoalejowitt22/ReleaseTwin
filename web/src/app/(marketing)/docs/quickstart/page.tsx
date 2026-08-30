import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { CodeBlock } from "@/components/code-block";

export const metadata: Metadata = {
  title: "Quickstart — ReleaseTwin",
  description: "Run the bundled HTTP example with no credentials, then write a case against your own API.",
};

export default function QuickstartPage() {
  return (
    <>
      <DocHeader
        title="Quickstart"
        lead="Run a real HTTP case against a live API in a few minutes. No credentials, no account."
      />

      <div className="mb-10">
        <object
          type="image/svg+xml"
          data="/demo-flag-proof.svg"
          aria-label="Terminal recording: a zero-credential HTTP case passing, then a flag-proof run reporting (Passed)"
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

      <DocSection title="1. Get the CLI">
        <P>Two ways to run it. With the .NET 8 SDK:</P>
        <CodeBlock
          label="clone and build"
          code={`git clone https://github.com/ernestoalejowitt22/ReleaseTwin
cd ReleaseTwin
dotnet build ReleaseTwin.sln`}
        />
        <P>Or via the published container image — no .NET SDK required:</P>
        <CodeBlock
          label="docker"
          code={`docker pull ghcr.io/ernestoalejowitt22/releasetwin/cli:0.1.0`}
        />
      </DocSection>

      <DocSection title="2. Run the bundled HTTP example">
        <P>
          It makes a real HTTP call to a public test API and runs two real JSONPath assertions —
          against the live internet, no fake handler, no setup.
        </P>
        <CodeBlock
          label="from source"
          code={`dotnet run --project src/ReleaseTwin.Cli -- examples/cases`}
        />
        <CodeBlock
          label="or with docker"
          code={`docker run --rm -v $(pwd)/examples:/workspace:ro \\
  ghcr.io/ernestoalejowitt22/releasetwin/cli:0.1.0`}
        />
        <P>Output:</P>
        <CodeBlock
          code={`FAIL CLM-042 (Infrastructure): missing-capability:http:azure-devops
FLAGPROOF FLAGPROOF-DEMO-1 (Ineligible): no installed adapter exposes feature-state control
PASS HTTP-DEMO-1
1 passed, 2 failed`}
        />
        <P>
          <code className="text-foreground">HTTP-DEMO-1</code> passed against the live API. The
          other two need Azure DevOps credentials and report as failing rather than crashing — a
          non-zero exit code is safe to wire straight into CI (
          <code className="text-foreground">... || exit 1</code>).
        </P>
      </DocSection>

      <DocSection title="3. Write a case against your own API">
        <P>
          No new adapter code needed — <code className="text-foreground">http.request</code> and{" "}
          <code className="text-foreground">http.assertJsonPath</code> test any REST API from
          case-file data alone. Put this in <code className="text-foreground">cases/my-case.yaml</code>:
        </P>
        <CodeBlock
          label="cases/my-case.yaml"
          code={`id: MY-CASE-1
oracle:
  locator: tickets/MY-CASE-1
fixture:
  locator: my-fixture.json       # resolved next to cases/, in a fixtures/ directory
  sha256: <sha256 of the fixture file>
pipeline:
  - operation: http.request
    with:
      method: POST
      url: \${API_BASE_URL}/orders     # \${ENV_VAR} resolved at load time — never commit secrets
      headers:
        Authorization: Bearer \${API_TOKEN}
      body:
        productId: 123
  - operation: http.assertJsonPath
    with:
      path: $.status
      expected: confirmed`}
        />
        <UL>
          <li>
            <code className="text-foreground">${"{ENV_VAR}"}</code> references are resolved when the
            case loads — keep URLs, tokens, and secrets in the environment, not the file.
          </li>
          <li>
            The fixture is resolved locally by whatever machine runs the CLI and verified by hash
            before the pipeline runs.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Next">
        <UL>
          <li>
            <Link href="/docs/case-files" className="text-primary underline underline-offset-4">
              Case files
            </Link>{" "}
            — every block a case can declare, including flag proof.
          </li>
          <li>
            <Link href="/docs/hosted-platform" className="text-primary underline underline-offset-4">
              Hosted platform
            </Link>{" "}
            — upload run history and turn on the evidence viewer.
          </li>
        </UL>
      </DocSection>
    </>
  );
}
