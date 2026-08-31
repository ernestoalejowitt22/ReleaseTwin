import type { Metadata } from "next";
import Link from "next/link";
import { DocHeader, DocSection, P, UL } from "@/components/doc";
import { LEGAL_CONTACT_EMAIL, LEGAL_ENTITY } from "@/lib/site";

export const metadata: Metadata = {
  title: "Terms of Service — ReleaseTwin",
  description:
    "Plain-language terms for the ReleaseTwin hosted dashboard. The CLI and adapters are separate — see the licenses in the repository.",
};

const UPDATED = "August 2026";

export default function TermsPage() {
  return (
    <>
      <DocHeader
        title="Terms of Service"
        lead={`These terms cover the ReleaseTwin hosted dashboard. Last updated ${UPDATED}. This is an early-access product and these terms will be reviewed by counsel before general availability.`}
      />

      <DocSection title="Who we are, what this covers">
        <P>
          &ldquo;We&rdquo; / &ldquo;us&rdquo; means {LEGAL_ENTITY}. &ldquo;Service&rdquo; means
          the hosted ReleaseTwin dashboard, its API, and the account system at this site.
        </P>
        <P>
          The ReleaseTwin <strong>CLI, execution kernel, and adapters are open source</strong> and
          governed by their own licenses in the source repository, not by these terms. You can run
          them with no account and these terms do not apply to that use.
        </P>
      </DocSection>

      <DocSection title="Your account">
        <UL>
          <li>You must be able to form a binding contract and use the Service only as permitted by law.</li>
          <li>You are responsible for activity under your account and for keeping your API tokens secret.</li>
          <li>One person or organization per account. Don&rsquo;t share credentials.</li>
          <li>Early access: features may change or break, and we may contact you for feedback.</li>
        </UL>
      </DocSection>

      <DocSection title="Your data">
        <P>
          Your test cases execute in <strong>your own</strong> infrastructure. By default the
          Service only receives run <em>metadata</em> — case identifiers, fixture hashes, pass/fail,
          failure classification, timestamps. It never receives your fixture content, request or
          response bodies, or credentials unless you explicitly opt a project into evidence upload,
          and that evidence is redacted by the CLI on your side before it is sent. See the{" "}
          <Link href="/docs/security" className="underline">Security page</Link> and the{" "}
          <Link href="/privacy" className="underline">Privacy Policy</Link>.
        </P>
        <UL>
          <li>You own your data. You can export or delete your projects and their reports at any time.</li>
          <li>
            We use your data to operate the Service (store and display your run history and
            evidence) and, in aggregate and de-identified form, to improve it.
          </li>
          <li>
            You must have the right to submit whatever you upload, and you must not upload anyone
            else&rsquo;s personal data or secrets through the evidence feature.
          </li>
        </UL>
      </DocSection>

      <DocSection title="Acceptable use">
        <P>Don&rsquo;t use the Service to:</P>
        <UL>
          <li>break the law, infringe others&rsquo; rights, or violate a third party&rsquo;s terms;</li>
          <li>attack, probe, or overload the Service or other users&rsquo; data;</li>
          <li>resell or operate the hosted Service for third parties (the hosted platform&rsquo;s license prohibits this);</li>
          <li>upload malware or use the Service to distribute it.</li>
        </UL>
      </DocSection>

      <DocSection title="Plans, billing, and changes">
        <UL>
          <li>The Free tier is free. Paid plans, when offered, are billed as stated at signup (annual unless noted).</li>
          <li>We may change prices or plan limits with reasonable notice; changes don&rsquo;t apply to a term you&rsquo;ve already paid for.</li>
          <li>You can cancel any time; paid time already elapsed is non-refundable unless required by law or our own guarantee for a specific engagement.</li>
          <li>We may modify or discontinue features. If we discontinue the hosted Service entirely, our published continuity commitment applies — see the <Link href="/docs/security" className="underline">Security page</Link>.</li>
        </UL>
      </DocSection>

      <DocSection title="Warranty, liability, indemnity">
        <P>
          The Service is provided <strong>&ldquo;as is&rdquo;</strong>, without warranties of any
          kind, to the maximum extent the law allows. We are not liable for indirect, incidental,
          or consequential damages, and our total liability for any claim is limited to the greater
          of the fees you paid us in the twelve months before the claim or US$100. You agree to
          indemnify us against claims arising from your misuse of the Service or your data.
        </P>
        <P>
          Nothing here limits liability that cannot be limited by law (for example, for our own
          fraud or willful misconduct).
        </P>
      </DocSection>

      <DocSection title="Termination">
        <P>
          You can stop using the Service and delete your account at any time. We may suspend or
          terminate an account that violates these terms or creates risk for the Service or other
          users; where practical we&rsquo;ll give notice and a chance to fix the problem first. On
          termination you can export your data for 30 days, after which it is deleted.
        </P>
      </DocSection>

      <DocSection title="Changes to these terms">
        <P>
          We&rsquo;ll post the updated date at the top and, for material changes, notify account
          holders by email before they take effect. Continuing to use the Service after that means
          you accept the change.
        </P>
      </DocSection>

      <DocSection title="Contact">
        <P>
          Questions about these terms: <a href={`mailto:${LEGAL_CONTACT_EMAIL}`} className="underline">{LEGAL_CONTACT_EMAIL}</a>.
        </P>
      </DocSection>
    </>
  );
}
