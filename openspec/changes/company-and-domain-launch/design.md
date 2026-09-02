## Context

See proposal.md — Why. The product is built; the company around it is a
placeholder. This change establishes a registered domain, company email, a
named legal operator, and the one product feature blocked on all of it: actually
delivering invitation emails.

Almost all of the work is external configuration (registrar, Google Workspace,
Polar, Clerk, DNS). The only code is a second `IInvitationEmailSender`
implementation and a sweep of placeholder URL/identity constants. The engine,
adapters, and execution path are untouched.

**Legal operator (updated 2026-09-02):** the operator is a **Mexican tax
resident, bootstrapping (no venture capital), already registered as a persona
física con actividad empresarial under RESICO.** That is a real legal operator
that can invoice domestic and foreign customers and receive USD/MXN — a US LLC
is *not* needed (no VC forcing function, no US-procurement requirement yet) and
would only add a US filing burden. `LEGAL_ENTITY` is set to the operator's
registered name now. Incorporation (S.A.S. or S. de R.L. de C.V.) is deferred to
a separate track, triggered by the RESICO persona-física revenue ceiling
(~3.5M MXN/yr), a customer's procurement demand, or legal advice — none of which
blocks a first pilot. The pre-pilot legal work that *does* matter is the
counsel review of the Terms of Service (governing law + limitation of liability)
and the AGPL/BSL licensing stack.

One external long pole gates ordering:

```
register releasetwin.com  ──► DNS (MX/SPF/DKIM/DMARC), Workspace, SES identity,
  (~1 hr)                      Clerk custom domain, NEXT_PUBLIC_SITE_URL, POLAR_*_URL

counsel review of ToS + licensing ──► GA readiness (does NOT block a pilot)
```

## Goals / Non-Goals

**Goals:**
- A real `SesInvitationEmailSender` bound by configuration, `LoggingInvitationEmailSender`
  as the fallback so local dev and tests are unaffected.
- SES domain identity + DKIM provisioned in Terraform (CI-only apply), with a
  Lambda-role `ses:SendEmail` statement scoped to the verified identity.
- Every placeholder URL/identity constant re-pointed to `releasetwin.com` and the named operator.
- A `docs/company-setup.md` recording what was registered where.

**Non-Goals:**
- Choosing a non-AWS email provider. SES is the default because it is the same
  AWS account, same OIDC deploy, no new vendor. Resend/Postmark stays a
  fallback only if SES domain verification proves impractical.
- A "resend invitation" UI beyond the link already in the API response
  (optional, tracked as a stretch task).
- Status page / SLA, `/blog`, D&O insurance, MSA template, SOC 2 — deferred
  (see proposal.md — Explicitly deferred).
- Flipping repos public or opening self-serve sign-up — that sequence is its
  own change (`go-public-sequence`).

## Decisions

### SES over Resend/Postmark
Same AWS account and OIDC deploy path, no new vendor contract, no new secret to
rotate. The Lambda already has an execution role Terraform manages; adding one
scoped `ses:SendEmail` statement is a smaller surface than an outbound HTTP
client plus an API key in Secrets Manager. **Alternative rejected:** Resend —
nicer DX and templating, but a second vendor relationship and a third-party
key for a solo operation to manage. Revisit only if SES sandbox removal or
domain verification stalls.

### Config-gated DI selection, Logging fallback stays the default
`SesInvitationEmailSender` is bound only when `Notifications:FromAddress` (and
the implied SES region/identity) is present; otherwise
`LoggingInvitationEmailSender` remains. This keeps `dotnet test` and local dev
zero-config and means the spec's "no provider configured" scenario is the
default test path. **Alternative rejected:** always bind SES with a "dry-run"
flag — more moving parts, and a misconfigured dry-run flag in prod silently
drops mail.

### Email failure is non-fatal to the invitation
The invitation row is written first; the send is attempted after and its
outcome logged. A provider error never rolls back the invitation, because the
accept link in the API response is a complete fallback path. This matches the
existing behavior where the link is surfaced to the admin regardless.

### `LEGAL_ENTITY` / `LEGAL_CONTACT_EMAIL` are the single swap point
`web/src/lib/site.ts` centralizes these. `LEGAL_ENTITY` is now the operator's
registered persona-física name; `LEGAL_CONTACT_EMAIL` moves to
`legal@releasetwin.com` once the mailbox exists. The `mailto:` links on the
security and pricing pages already reference these constants, so a future
entity/email change is one edit.

### Legal operator: named persona física now, incorporation deferred
The operator is a Mexican tax resident, bootstrapping, already registered as a
persona física con actividad empresarial (RESICO). That is sufficient to invoice
customers (CFDI domestically; a plain invoice for foreign customers, export of
services often 0% IVA) and take Merchant-of-Record payouts from Polar. **A US LLC
is rejected:** no venture capital (the usual forcing function), customers "could
be anyone" so no specific US-procurement requirement, and it adds a US Form 5472
filing burden ($25k penalty for a mistake) plus cross-border tax complexity for a
solo MX operator. **Incorporation (S.A.S. / S. de R.L. de C.V.) is a deferred
track**, triggered by the RESICO persona-física ceiling (~3.5M MXN/yr), a
customer's procurement demand, or legal advice — ask a Mexican contador +
tech lawyer which form fits (S.A.S. is a free online single-shareholder company
with limited liability; S. de R.L. is the traditional small-company form). None
of this blocks a first pilot.

### The liability shield is mostly the contract, not the entity
For "a test run broke a customer's production system," what protects the operator
is the Terms of Service — "as is", no consequential damages, liability capped at
fees paid / US$100, customer indemnity, and a governing-law/dispute clause. That
draft already exists (`web/src/app/(marketing)/terms/page.tsx`) and needs a
few hours of counsel review for the governing-law clause (Mexican law is the
cheap-to-enforce default for the operator) and the liability wording. A DPA and a
short design-partner/pilot agreement are worth adding at the same time — all
adaptable from a free standardized template (Common Paper / Bonterms) rather than
drafted from scratch.

### Domain-dependent config is enumerated, not automated
`NEXT_PUBLIC_SITE_URL` (Vercel), `WEB_BASE_URL` + `Api__PublicUrl` (repo vars),
the four `POLAR_*_URL` repo vars, and the Clerk custom domain are all set in
vendor dashboards / GitHub settings. There is no code path to set them; tasks.md
lists each with the exact value and location. `Api__PublicUrl` already
self-heals from `terraform output function_url`, so it needs only verification.

### DNS records are Terraform-managed, not emitted as outputs
The proposal assumed the domain would be at an arbitrary registrar, so the SES
DKIM / MAIL FROM records would be Terraform *outputs* the user pastes into a
registrar panel. Registering through Route 53 Domains changes that: the hosted
zone is in the same AWS account Terraform already drives, so
`hosted/terraform/dns-and-email.tf` looks the zone up with a
`data "aws_route53_zone"` and manages the DKIM CNAMEs, `_amazonses`
verification TXT, custom MAIL FROM (MX + SPF), and DMARC as `aws_route53_record`
resources directly — the CLAUDE.md "code over standing manual config" rule.
The zone itself is a data lookup, not a managed resource: Route 53 Domains
auto-creates it at registration and a second `aws_route53_zone` for the same
name would create a detached duplicate.

### Deploy role needs new permissions — bootstrap change ships in the same PR
The `releasetwin-github-actions-deploy` role is strictly resource-scoped and had
no SES or Route 53 access. `hosted/terraform-bootstrap/main.tf` gains a
`SesDomainIdentity` (`ses:*` on `*` — SES v1 identity APIs are account-global and
take no resource constraint; the *send* permission on the function stays pinned
to the one identity ARN) and a `Route53Records` statement. `bootstrap.yml` and
`deploy-hosted.yml` both auto-apply on merge; a first-run race where deploy
beats bootstrap is fixed by re-running deploy (incremental apply). This is the
pattern `deploy-hosted.yml`'s own header comment already documents.

### The whole SES/DNS block is gated on `domain_name`
Empty `domain_name` ⇒ no zone lookup, no identity, no records, no IAM statement —
and `notifications_from_address` empty ⇒ `LoggingInvitationEmailSender` stays
bound. So merging is a guaranteed no-op; the user flips `DOMAIN_NAME` then
`NOTIFICATIONS_FROM_ADDRESS` repo vars to activate it in two deliberate steps.
An SES domain identity is an account+region singleton, so only one stack may set
`domain_name` (today: the `releasetwin-dev-` auto-deploy stack).

## Risks / Trade-offs

- **SES starts in sandbox mode (only verified recipients).** → Request
  production access as an explicit task before relying on invites to arbitrary
  addresses; until granted, verify the first design partner's address by hand.
- **DKIM/DMARC misconfiguration silently lands invites in spam.** → Task
  includes a deliverability check (mail-tester or equivalent) against a real
  external inbox before declaring the feature done.
- **Persona física = unlimited personal liability.** → Mitigated primarily by the
  ToS (as-is, liability cap, indemnity, governing law) + the product design
  (runs in the customer's infra, metadata-only by default). Incorporate (S.A.S. /
  S. de R.L.) when revenue nears the RESICO ceiling or a customer/lawyer requires
  it — deferred track, not a pilot blocker.
- **Counsel review is still pending.** → The ToS page itself says "reviewed by
  counsel before general availability". A pilot can run on the current draft +
  a short pilot agreement; GA waits on the review (also covers the AGPL/BSL
  licensing stack — `go-public-sequence` §2.3).
- **Trademark conflict on "ReleaseTwin" surfaces after registration.** → A
  quick USPTO/EUIPO/IMPI glance is a task; a rename is still relatively cheap at
  this stage.
- **Polar production cutover with wrong price IDs charges real money wrong.** →
  Polar sandbox e2e (`docs/billing-sandbox-runbook.md`) is a prerequisite task;
  `POLAR_UPGRADE_ENABLED` stays `false` until that passes.

## Migration Plan

1. Trademark glance → register `releasetwin.com`.
2. DNS + Google Workspace + SES identity (Terraform) in parallel.
3. `SesInvitationEmailSender` + DI gate + tests; deploy via CI; `Notifications:FromAddress`
   set once SES identity is verified.
4. Re-point domain-dependent config (Vercel, repo vars, Clerk custom domain).
5. `LEGAL_ENTITY` = the operator's persona-física name (done); Polar production
   payee = the persona física / RFC. Counsel review of the ToS + licensing stack
   (background, gates GA not the pilot). Incorporation deferred.
6. `docs/company-setup.md` + README brand-line update.

Rollback: the DI gate means dropping `Notifications:FromAddress` reverts to
logging-only delivery with no redeploy required. Config re-pointing is
reversible in each dashboard. Domain and LLC are not rolled back.

## Open Questions

- Which single inbox do `hello@ / security@ / billing@ / legal@` forward to —
  a Workspace user or a group? (Does not change specs or tasks; a Workspace
  admin setting.)
- Is the optional "resend invitation" affordance worth including in this change
  or deferred to a follow-up? (Left as a stretch task, unchecked.)
