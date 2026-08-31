import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { CodeBlock } from "@/components/code-block";

export const metadata: Metadata = {
  title: "Case files — ReleaseTwin",
  description: "The YAML a case is made of: oracle, fixture, pipeline, preconditions, cleanup, and flag proof.",
};

export default function CaseFilesPage() {
  return (
    <>
      <DocHeader
        title="Case files"
        lead="A case is a YAML file describing one release-critical check: what to run, what fixture it depends on, how to tell pass from fail, and how to clean up."
      />

      <DocSection title="Anatomy">
        <CodeBlock
          label="cases/example.yaml"
          code={`id: MY-CASE-2
release: "4.2"                      # optional — groups this case in the release rollup
oracle:
  locator: tickets/MY-CASE-2        # where the human-readable expectation lives
fixture:
  locator: my-fixture.json          # resolved in a fixtures/ dir next to cases/
  sha256: <sha256 of the fixture file>
requires:
  - http:azure-devops               # capabilities this case needs installed
preconditions:
  - check: azdo.areaPathExists
    owner: QA claims fixtures        # who owns fixing this if it's not satisfied
pipeline:
  - operation: azdo.createWorkItem
  - operation: azdo.getWorkItem
cleanup:
  - operation: azdo.deleteWorkItem
resource_key: 'TeamProject\\Area'    # optional — serializes cases sharing this key`}
        />
      </DocSection>

      <DocSection title="Blocks">
        <UL>
          <li>
            <code className="text-foreground">oracle.locator</code> — a reference to where the
            expected behavior is documented. Carried through into every report.
          </li>
          <li>
            <code className="text-foreground">fixture</code> — a locator plus a SHA-256. The file
            is read locally and hashed before the pipeline runs; a mismatch fails the case
            immediately.
          </li>
          <li>
            <code className="text-foreground">requires</code> — capability strings that must be
            provided by an installed adapter, or the case reports{" "}
            <code className="text-foreground">missing-capability</code> rather than crashing.
          </li>
          <li>
            <code className="text-foreground">preconditions</code> — three-state checks
            (satisfied / not satisfied / inconclusive), each naming an{" "}
            <code className="text-foreground">owner</code>.
          </li>
          <li>
            <code className="text-foreground">pipeline</code> — ordered operations. State from one
            step (an ID, a token) is available to later steps and to cleanup.
          </li>
          <li>
            <code className="text-foreground">cleanup</code> — runs even if the pipeline fails;
            no-ops safely when there is nothing to undo.
          </li>
          <li>
            <code className="text-foreground">resource_key</code> — optional; cases sharing a key
            run serially instead of in parallel.
          </li>
          <li>
            <code className="text-foreground">release</code> — optional free-form label (a version,
            a sprint, an epic key). It has no effect on execution; the hosted platform groups cases
            by it into a per-release readiness rollup.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Operations available today">
        <UL>
          <li>
            <strong className="text-foreground">Generic HTTP</strong> (any REST API):{" "}
            <code className="text-foreground">http.request</code>,{" "}
            <code className="text-foreground">http.assertJsonPath</code> — fully data-driven from
            the case file.
          </li>
          <li>
            <strong className="text-foreground">Azure DevOps</strong> (fixed-shape):{" "}
            <code className="text-foreground">azdo.createWorkItem</code>,{" "}
            <code className="text-foreground">azdo.getWorkItem</code>,{" "}
            <code className="text-foreground">azdo.transitionWorkItemState</code>,{" "}
            <code className="text-foreground">azdo.areaPathExists</code>,{" "}
            <code className="text-foreground">azdo.deleteWorkItem</code>,{" "}
            <code className="text-foreground">azdo.readFeatureVariable</code>.
          </li>
          <li>
            <strong className="text-foreground">UI</strong> (Chromium via Playwright, opt-in with{" "}
            <code className="text-foreground">RELEASETWIN_UI_ENABLED=1</code>):{" "}
            <code className="text-foreground">ui.navigate</code>,{" "}
            <code className="text-foreground">ui.click</code>,{" "}
            <code className="text-foreground">ui.fill</code>,{" "}
            <code className="text-foreground">ui.waitFor</code>,{" "}
            <code className="text-foreground">ui.assertVisible</code>,{" "}
            <code className="text-foreground">ui.setCookie</code>.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Flag proof">
        <P>
          Add a <code className="text-foreground">flag_proof</code> block to a case that also has a
          feature-state adapter configured. The CLI runs the case twice — once with the feature
          off, once on — and reports a single discriminating outcome instead of a plain pass/fail.
        </P>
        <CodeBlock
          label="added to any case"
          code={`flag_proof:
  feature_key: release-proof-feature   # the flag to toggle
  build_identity: build-123            # carried through the report`}
        />
        <P>
          The outcome is <code className="text-foreground">Passed</code> when the case&apos;s own
          pipeline correctly tells known-bad from known-good — or{" "}
          <code className="text-foreground">WeakOracle</code> /{" "}
          <code className="text-foreground">BothFailed</code> /{" "}
          <code className="text-foreground">Inverted</code> /{" "}
          <code className="text-foreground">Ineligible</code> when it can&apos;t. Today only Azure
          DevOps&apos;s variable-group controller can drive the toggle.
        </P>
      </DocSection>

      <DocSection title="Next">
        <P>
          <Link href="/docs/hosted-platform" className="text-primary underline underline-offset-4">
            Connect the hosted platform
          </Link>{" "}
          to keep run history and turn on the evidence viewer.
        </P>
      </DocSection>
    </>
  );
}
