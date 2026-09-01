import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { CodeBlock } from "@/components/code-block";
import { SECURITY_CONTACT_EMAIL } from "@/lib/site";

export const metadata: Metadata = {
  title: "Security & credentials — ReleaseTwin",
  description:
    "How ReleaseTwin handles credentials: local execution, ${ENV_VAR} resolution, opaque API tokens, CLI-side redaction, and encrypted-at-rest stored secrets.",
};

export default function SecurityPage() {
  return (
    <>
      <DocHeader
        title="Security & credentials"
        lead="Where every secret lives, what crosses the network, and what the operator can and cannot see."
      />

      <DocSection title="The short version">
        <UL>
          <li>
            Your cases execute on your own machine or CI runner. Credentials, fixtures, and
            response bodies stay there.
          </li>
          <li>
            By default only report <strong className="text-foreground">metadata</strong> is
            uploaded — the ingest contract has no field that can carry a credential.
          </li>
          <li>
            Evidence upload is opt-in, and its redaction runs in your CLI before anything is
            sent.
          </li>
          <li>
            Optionally storing credentials in the hosted platform (a Paid convenience) encrypts
            them at rest under keys the operator manages but cannot use to read your plaintext
            out of a database dump alone.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Credentials in case files: ${ENV_VAR}, never literals">
        <P>
          Case files are meant to be committed to your repo. They never contain secret values —
          only <code className="text-foreground">${"{ENV_VAR}"}</code> references, resolved from
          the environment when the case loads:
        </P>
        <CodeBlock
          label="cases/order.yaml"
          code={`pipeline:
  - operation: http.request
    with:
      url: \${API_BASE_URL}/orders
      headers:
        Authorization: Bearer \${API_TOKEN}`}
        />
        <UL>
          <li>
            The <code className="text-foreground">${"${...}"}</code> pattern accepts only{" "}
            <code className="text-foreground">A–Z</code>, <code className="text-foreground">0–9</code>{" "}
            and <code className="text-foreground">_</code>.
          </li>
          <li>
            A reference to an <strong className="text-foreground">undefined</strong> variable is a
            hard load error — the case does not run with a blank or literal placeholder.
          </li>
          <li>
            Fixture locators are path-contained: no <code className="text-foreground">..</code>,
            no absolute paths, no escaping the <code className="text-foreground">fixtures/</code>{" "}
            root. The file is read locally and verified by SHA-256 before the pipeline runs.
          </li>
        </UL>
      </DocSection>

      <DocSection title="API tokens">
        <P>
          A project token is what the CLI presents to upload results (and, if you use them, to
          fetch stored secrets). Issued from the dashboard:
        </P>
        <UL>
          <li>
            Format <code className="text-foreground">rtw_</code> followed by 256 bits of
            cryptographic randomness.
          </li>
          <li>
            The server stores <strong className="text-foreground">only a SHA-256 hash</strong> of
            the token, plus a short display prefix. The raw value is shown once, at creation, and
            never again.
          </li>
          <li>
            Scoped to a single project — a token issued for project A cannot read or write any
            other project&apos;s data, including other projects in the same organization.
          </li>
          <li>Revocable from the dashboard; a revoked token is rejected immediately.</li>
          <li>
            A token and a web-session credential (a Clerk JWT) are different auth domains: a web
            JWT cannot call the ingest API, and an API token cannot act as a web session.
          </li>
        </UL>
        <P>
          Keep the token in your CI&apos;s secret store and pass it as{" "}
          <code className="text-foreground">RELEASETWIN_API_TOKEN</code>. Traffic to the hosted
          API is HTTPS.
        </P>
      </DocSection>

      <DocSection title="What is uploaded, and what cannot be">
        <P>Default upload — metadata only:</P>
        <UL>
          <li>case ID, oracle reference, fixture hash, pass/fail, failure classification, cleanup status, timing</li>
          <li>the paired-leg summary for a flag-proof run</li>
        </UL>
        <P>
          This is a property of the contract, not a policy: the ingest payload schema{" "}
          <strong className="text-foreground">defines no field</strong> capable of carrying
          fixture content, operation response bodies, or a credential. A malformed payload is
          rejected in full, with nothing partially stored.
        </P>
      </DocSection>

      <DocSection title="Evidence redaction (opt-in)">
        <P>
          Turn on evidence capture per project and a run also produces a structured document —
          per-step request/response summaries, assertion path / expected / observed, UI
          screenshots. Before any of it is uploaded, the CLI redacts it on the machine that ran
          the case. Un-redacted evidence is never transmitted under any configuration.
        </P>
        <P>Redaction is a three-layer model, applied in order:</P>
        <UL>
          <li>
            <strong className="text-foreground">Built-in denylist</strong> — removes{" "}
            <code className="text-foreground">Authorization</code> and{" "}
            <code className="text-foreground">Cookie</code> headers, credential-shaped fields, and
            any value equal to a secret or token that was resolved during the run (so a secret
            that echoes back in a response body is masked there too).
          </li>
          <li>
            <strong className="text-foreground">Per-case denylist</strong> — additional field
            names, headers, JSONPath expressions, or UI selectors/regions you name.
          </li>
          <li>
            <strong className="text-foreground">Per-case allowlist</strong> — lets you keep a
            specific field a built-in rule would otherwise drop.{" "}
            <em>It cannot re-enable anything the built-in denylist removed.</em>
          </li>
        </UL>
        <P>
          The redactor fails closed: a rule it cannot evaluate results in masking, not exposure.
          The ingest API stores the document opaquely — it never inspects or re-strips it — under
          a per-project retention window (default 30 days, max 365), and a daily purge deletes
          expired evidence while leaving the metadata report intact.
        </P>
      </DocSection>

      <DocSection title="Stored credentials & project secrets (Paid convenience)">
        <P>
          Rather than wiring the same environment variables everywhere the CLI runs, you can
          store adapter credentials and arbitrary named secrets per project through the
          dashboard. The CLI then fetches them at run time using its project-scoped API token.
          This is entirely optional — the <code className="text-foreground">${"{ENV_VAR}"}</code>{" "}
          path never goes away.
        </P>
        <UL>
          <li>
            Values are encrypted at rest with ASP.NET Core Data Protection. The key ring is
            persisted to AWS Systems Manager Parameter Store, not to the application database.
          </li>
          <li>
            Adapter credentials, project secrets, and connection state each use a separate
            protector purpose, so a payload from one can never be decrypted as another.
          </li>
          <li>
            Once set, a value is never redisplayed — the dashboard only shows that a credential
            exists and its non-secret metadata.
          </li>
          <li>
            Rotate or revoke any time, without operator involvement. A revoked value is not
            returned by a later CLI fetch.
          </li>
          <li>
            Storing secrets requires the Paid tier; the failure is distinguishable from an auth
            error.
          </li>
        </UL>
      </DocSection>

      <DocSection title="What the operator can and cannot see">
        <UL>
          <li>
            <strong className="text-foreground">Cannot see</strong>: your fixtures, request and
            response bodies, or any un-redacted evidence — none of it leaves your infrastructure.
          </li>
          <li>
            <strong className="text-foreground">Cannot recover</strong> from a database dump
            alone: your API tokens (only hashes are stored) or your stored secret values
            (ciphertext; keys live in a separate Parameter Store path).
          </li>
          <li>
            <strong className="text-foreground">Can see</strong>: the metadata you upload — case
            IDs, hashes, pass/fail, classifications, timing — and, if you enabled it, the
            evidence document exactly as your CLI redacted it.
          </li>
          <li>Sign-up needs no human approval; there is no operator in your critical path.</li>
        </UL>
      </DocSection>

      <DocSection title="Continuity — what happens if we stop">
        <P>
          ReleaseTwin is built and run by a very small independent team. The design makes that a
          non-issue for your releases:
        </P>
        <UL>
          <li>
            The CLI, execution kernel, and adapters are open source (AGPL-3.0) and run entirely
            in your own infrastructure. They keep working with no account and no network call to
            us — a hosted outage, or the hosted platform going away entirely, never blocks a
            release.
          </li>
          <li>
            An organization admin can download the full run history and stored evidence at any
            time from the dashboard, as a single ZIP with a{" "}
            <a
              href="https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/docs/data-export.md"
              className="underline"
            >
              documented format
            </a>{" "}
            — no proprietary lock-in on the data itself.
          </li>
          <li>
            If we wind the company down, active hosted licenses convert to perpetual for their
            remaining term and the hosted source is published so a customer or third party can
            self-host it.
          </li>
          <li>
            Payments run through a Merchant of Record (Polar): card and billing-address data are
            entered only on Polar&rsquo;s hosted checkout and portal, never seen or stored by us,
            and Polar issues invoices and remits sales tax. A lapsed subscription degrades hosted
            entitlements on a published grace schedule but never deletes your uploaded evidence.
          </li>
        </UL>
        <P>
          This is a deliberate commitment, not just a side effect of open-sourcing the core —
          it is the answer to the fair question &ldquo;what if this two-person company
          disappears.&rdquo;
        </P>
      </DocSection>

      <DocSection title="Reporting a vulnerability">
        <P>
          Email{" "}
          <a
            href={`mailto:${SECURITY_CONTACT_EMAIL}?subject=ReleaseTwin%20security`}
            className="text-primary underline underline-offset-4"
          >
            {SECURITY_CONTACT_EMAIL}
          </a>{" "}
          with details and a proof of concept. Please don&apos;t open a public issue for a
          security report.
        </P>
        <P>
          See also{" "}
          <Link href="/docs/hosted-platform" className="text-primary underline underline-offset-4">
            Hosted platform
          </Link>{" "}
          for the upload flow end to end.
        </P>
      </DocSection>
    </>
  );
}
