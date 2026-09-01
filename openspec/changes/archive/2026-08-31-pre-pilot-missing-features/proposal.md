## Why

An exploration (2026-08-31) of what's missing "project-wise" before a design
partner found two kinds of gap:

1. **Promise-vs-reality gaps** — public copy claims things the product doesn't
   do. The marketing security page and `docs/continuity.md` both state that
   *"your evidence and run history are exportable at any time, in a documented
   format"* — **no export endpoint exists**. The same doc references *"the status
   page and SLA terms"* — neither exists. These are the dangerous gaps: a
   security-conscious buyer reads the continuity commitment (which the project
   positions as its differentiator vs funded competitors), then asks to see the
   export, and it isn't there.
2. **Deferred product features** — known, un-built, and correctly deferred until a
   real workflow demands them (listed below, not in scope).

This change closes the promise-vs-reality gaps: it makes the continuity
commitment real. Nothing here depends on the domain / company / legal work, which
is a separate follow-up track.

## What Changes

- **New `data-export` capability.** A hosted, authenticated endpoint that returns
  an organization's run history and stored evidence documents as a single
  documented JSON archive — the thing `docs/continuity.md` and the security page
  already promise. Scoped to the caller's active organization, admin-gated,
  metadata + already-redacted evidence only (never fixture content or secrets —
  the same contract the ingest side already enforces). The response format is
  documented so a customer can consume it without ReleaseTwin.
- **Reconcile the continuity commitment with reality.** Update `docs/continuity.md`
  and the marketing security page so every commitment is either (a) backed by
  implemented behavior (the new export), or (b) explicitly scoped as roadmap with
  no false present-tense claim. Specifically: point the export claim at the real
  endpoint; drop or reword the references to a status page and SLA terms that do
  not exist yet.

## Capabilities

### New Capabilities

- `data-export`: an organization owner can pull their full run history and stored
  evidence out of the hosted platform in one documented, self-describing JSON
  archive, at any time, with no ReleaseTwin-proprietary transformation to reverse.

### Modified Capabilities

<!-- No spec-level requirement change to marketing-site: the security/continuity
     page is content, not a governed contract, and the doc reconciliation is a
     wording fix tracked in tasks. -->

## Impact

- **hosted API:** one new authenticated endpoint (`GET /api/export` or similar);
  reuses `IOrganizationAccessGuard` (`ViewEvidence` or a dedicated capability),
  the case/flag-proof report repositories, the run-evidence repository, and the
  evidence blob store. Large archives — decide streaming vs. size cap in design.
- **web/:** a "Download your data" control on the dashboard (or account settings);
  BFF proxies the archive stream.
- **docs:** `docs/continuity.md` + `web/src/app/(marketing)/docs/security/page.tsx`
  wording pass; a short `docs/data-export.md` documenting the archive format.
- **no** new infra, no new external dependency, no domain dependency.

## Explicitly deferred (not in this change)

Revisit each when a design partner's workflow makes it concrete:

- **Real invitation email delivery** — `IInvitationEmailSender` is a logging stub;
  a real sender (SES / Resend) needs a verified sending domain → belongs to the
  domain / company follow-up track.
- **Status page + SLA** — beyond the copy fix here, an actual uptime page and SLA
  terms are their own effort (and a status subdomain wants the domain).
- **CLI distribution:** a `dotnet tool`-installable global NuGet package, and a
  Homebrew tap. The container image + `releasetwin init` scaffold + GitHub Action
  already exist; the .NET-native install path does not.
- **Deeper PR integration:** the GitHub Action renders a run summary onto a PR
  today; a commit status check and a link to the hosted evidence drill-down are
  extensions.
- **SSO / SAML**, **audit log** — enterprise, later.
- **Signed / tamper-evident evidence** — a cryptographic attestation over the
  evidence document; a real "evidence product" differentiator, no partner asking
  yet.
- **Non-REST adapter** (message queue / database / gRPC), **hosted fixture store**
  — scope to the specific design partner that needs them.
