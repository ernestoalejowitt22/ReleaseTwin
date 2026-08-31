import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { LEGAL_CONTACT_EMAIL, LEGAL_ENTITY } from "@/lib/site";

export const metadata: Metadata = {
  title: "Privacy Policy — ReleaseTwin",
  description:
    "What the ReleaseTwin hosted dashboard collects, why, and how execution and your test data stay in your own infrastructure.",
};

const UPDATED = "August 2026";

export default function PrivacyPage() {
  return (
    <>
      <DocHeader
        title="Privacy Policy"
        lead={`How ${LEGAL_ENTITY} handles data for the hosted ReleaseTwin dashboard. Last updated ${UPDATED}. A counsel review is planned before general availability.`}
      />

      <DocSection title="The short version">
        <UL>
          <li>Your test cases run in <strong>your</strong> infrastructure. We don&rsquo;t execute them and we don&rsquo;t receive your test data by default.</li>
          <li>What we store: your account details, your projects, and the <em>metadata</em> of runs you upload.</li>
          <li>Evidence (request/response detail, screenshots) is only stored if you opt a project in, and it&rsquo;s redacted by the CLI on your side first.</li>
          <li>We don&rsquo;t sell personal data and we don&rsquo;t run ad tracking.</li>
        </UL>
      </DocSection>

      <DocSection title="What we collect">
        <P><strong>Account &amp; identity</strong> — via our authentication provider (Clerk): your email, name if you provide one, and authentication events. Passwords are handled by the provider; we never see them.</P>
        <P><strong>Project &amp; usage data</strong> — the projects, journeys, API tokens, and settings you create; and per run: case identifiers, oracle references, fixture hashes, pass/fail, failure classification, flag-proof outcome, and timestamps. Counts of uploaded runs and active projects, for plan metering.</P>
        <P><strong>Evidence documents (opt-in only)</strong> — if you enable evidence upload for a project, per-step request/response summaries, assertion detail, and screenshots. These are redacted in your own CLI before upload (auth headers, credential-shaped fields, resolved secrets, and your own masking rules) and stored opaquely — our systems don&rsquo;t parse them.</P>
        <P><strong>Operational data</strong> — server logs, IP address and user agent on requests, and error diagnostics, kept for security and debugging.</P>
        <P><strong>Marketing site</strong> — if analytics are enabled they are privacy-respecting and cookie-free (aggregate page counts, no cross-site tracking, no profiles).</P>
      </DocSection>

      <DocSection title="What we do not collect">
        <UL>
          <li>Your fixture file contents, request or response bodies, or credentials — the default upload path has no field for them, and they never leave your infrastructure unless you turn on evidence upload.</li>
          <li>Your source code or CI configuration.</li>
          <li>Behavioral advertising data. There is none.</li>
        </UL>
      </DocSection>

      <DocSection title="Why we use it">
        <UL>
          <li>To provide the Service — authenticate you, store and display your run history and evidence, enforce plan limits.</li>
          <li>To keep it secure — detect abuse, investigate incidents.</li>
          <li>To improve it — in aggregate and de-identified form; and to contact early-access users for feedback.</li>
          <li>To bill you, if you&rsquo;re on a paid plan, through our payment processor.</li>
        </UL>
        <P>Legal bases (where the GDPR applies): performance of our contract with you, our legitimate interests in a secure and improving product, and your consent where required.</P>
      </DocSection>

      <DocSection title="Who we share it with">
        <P>Only the sub-processors needed to run the Service, each under a data-processing agreement:</P>
        <UL>
          <li>our cloud host (compute, database, object storage);</li>
          <li>our authentication provider;</li>
          <li>our payment processor (paid plans only);</li>
          <li>our email provider (transactional and, if you opt in, product email).</li>
        </UL>
        <P>We don&rsquo;t sell or rent personal data. We may disclose data if legally required, and we&rsquo;ll tell you unless prohibited.</P>
      </DocSection>

      <DocSection title="Retention">
        <UL>
          <li>Account data: while your account is open, then deleted within 30 days of account deletion.</li>
          <li>Run metadata: until you delete the project, or per your plan&rsquo;s retention setting.</li>
          <li>Evidence documents: your project&rsquo;s retention window (default 30 days, up to 365), enforced by a daily purge; the metadata report survives the evidence.</li>
          <li>Logs: a rolling window measured in weeks.</li>
        </UL>
      </DocSection>

      <DocSection title="Your rights">
        <P>
          You can access, export, correct, or delete your data from the dashboard, or by emailing{" "}
          <a href={`mailto:${LEGAL_CONTACT_EMAIL}`} className="underline">{LEGAL_CONTACT_EMAIL}</a>.
          Depending on where you live you may also have the right to object to or restrict
          processing, or to lodge a complaint with a supervisory authority. We&rsquo;ll respond
          within the time the applicable law requires.
        </P>
      </DocSection>

      <DocSection title="International transfers & security">
        <P>
          The Service is hosted in the United States; using it involves transferring your data
          there. We rely on standard contractual clauses with sub-processors where required. Data
          is encrypted in transit and at rest; API tokens and stored project secrets are encrypted
          at rest and shown only once. See the <Link href="/docs/security" className="underline">Security page</Link> for detail.
        </P>
      </DocSection>

      <DocSection title="Children">
        <P>The Service isn&rsquo;t for anyone under 16, and we don&rsquo;t knowingly collect their data.</P>
      </DocSection>

      <DocSection title="Changes & contact">
        <P>
          We&rsquo;ll post the updated date above and email account holders before a material
          change takes effect. Privacy questions or requests:{" "}
          <a href={`mailto:${LEGAL_CONTACT_EMAIL}`} className="underline">{LEGAL_CONTACT_EMAIL}</a>.
        </P>
      </DocSection>
    </>
  );
}
