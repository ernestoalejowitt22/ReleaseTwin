## Context

See proposal.md — Why. The product is built; the company around it is a
placeholder. This change establishes a registered domain, company email, a
legal entity, and the one product feature blocked on all of it: actually
delivering invitation emails.

Almost all of the work is external configuration (registrar, Google Workspace,
Stripe Atlas, Polar, Clerk, DNS). The only code is a second
`IInvitationEmailSender` implementation and a sweep of placeholder
URL/identity constants. The engine, adapters, and execution path are
untouched.

Two external long poles gate ordering:

```
register releasetwin.com  ─┬─► DNS (MX/SPF/DKIM/DMARC), Workspace, SES identity,
  (~1 hr)                   │   Clerk custom domain, NEXT_PUBLIC_SITE_URL, POLAR_*_URL
                            │
form US LLC ──► EIN ────────┴─► LEGAL_ENTITY swap, Polar production payee, legal review
  (~1–2 wk)
```

## Goals / Non-Goals

**Goals:**
- A real `SesInvitationEmailSender` bound by configuration, `LoggingInvitationEmailSender`
  as the fallback so local dev and tests are unaffected.
- SES domain identity + DKIM provisioned in Terraform (CI-only apply), with a
  Lambda-role `ses:SendEmail` statement scoped to the verified identity.
- Every placeholder URL/identity constant re-pointed to `releasetwin.com` and the LLC.
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
`web/src/lib/site.ts` already centralizes these (currently
`"the ReleaseTwin project"` / `ernestoalejo22@gmail.com`). The `mailto:` links
on the security and pricing pages should be migrated to reference these
constants rather than hard-coding the address again, so a future entity/email
change is one edit.

### Domain-dependent config is enumerated, not automated
`NEXT_PUBLIC_SITE_URL` (Vercel), `WEB_BASE_URL` + `Api__PublicUrl` (repo vars),
the four `POLAR_*_URL` repo vars, and the Clerk custom domain are all set in
vendor dashboards / GitHub settings. There is no code path to set them; tasks.md
lists each with the exact value and location. `Api__PublicUrl` already
self-heals from `terraform output function_url`, so it needs only verification.

## Risks / Trade-offs

- **SES starts in sandbox mode (only verified recipients).** → Request
  production access as an explicit task before relying on invites to arbitrary
  addresses; until granted, verify the first design partner's address by hand.
- **DKIM/DMARC misconfiguration silently lands invites in spam.** → Task
  includes a deliverability check (mail-tester or equivalent) against a real
  external inbox before declaring the feature done.
- **LLC formation timeline is outside our control (~1–2 wk).** → Everything
  domain-gated proceeds in parallel; only `LEGAL_ENTITY` swap, Polar production
  payee, and legal review wait on the EIN. None of those block a first pilot on
  the dev stack.
- **Trademark conflict on "ReleaseTwin" surfaces after registration.** → A
  quick USPTO/EUIPO glance is the first task; the README already frames the
  brand as provisional, so a rename is still cheap at this stage.
- **Polar production cutover with wrong price IDs charges real money wrong.** →
  Polar sandbox e2e (`docs/billing-sandbox-runbook.md`) is a prerequisite task;
  `POLAR_UPGRADE_ENABLED` stays `false` until that passes.

## Migration Plan

1. Trademark glance → register `releasetwin.com`.
2. DNS + Google Workspace + SES identity (Terraform) in parallel.
3. `SesInvitationEmailSender` + DI gate + tests; deploy via CI; `Notifications:FromAddress`
   set once SES identity is verified.
4. Re-point domain-dependent config (Vercel, repo vars, Clerk custom domain).
5. LLC filing (background) → EIN → `LEGAL_ENTITY` swap, Polar production payee,
   legal review.
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
