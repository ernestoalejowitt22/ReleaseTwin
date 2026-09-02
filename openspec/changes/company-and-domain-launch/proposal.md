## Why

The product is built; the company around it is a placeholder. A design partner's
security / procurement review, in its first ten minutes, sees:

- the marketing site on `releasetwin.vercel.app`
- `ernestoalejo22@gmail.com` on the security page, the pricing page, and in the
  Terms / Privacy contact
- Terms that name **"the ReleaseTwin project"** — not a named legal operator — as
  the counterparty
- auth pages on `classic-marlin-8065.clerk.accounts.dev`

Each of those is a reason to walk. This change establishes a real identity:
a registered domain, company email, a named legal operator, and the one product
feature that was blocked on all of it — actually delivering invitation emails
(today `IInvitationEmailSender` is a logging stub and an admin copies the link by
hand).

`releasetwin.com` is available (checked 2026-08-31). The brand hedge in the
README (*"provisional brand is Validuo"*) is resolved by registering it.

**Legal operator (revised 2026-09-02):** the operator is a Mexican tax resident,
bootstrapping (no venture capital), and is **already registered as a persona
física con actividad empresarial under RESICO** — a legal operator that can
invoice and be paid. A US LLC is not needed and is not pursued. `LEGAL_ENTITY` is
set to that registered name. Incorporating a Mexican entity (S.A.S. / S. de R.L.)
is a deferred track keyed to the RESICO revenue ceiling or a customer/legal
trigger. The pre-pilot legal work is counsel review of the Terms of Service
(governing law + liability cap) and the AGPL/BSL licensing stack, plus a short
design-partner agreement — not incorporation.

## What Changes

**External setup (mostly manual — no code path; see "Manual steps"):**

- Register **`releasetwin.com`** (after a quick trademark glance); drop the
  "Validuo" hedge from the README and docs.
- Stand up **company email** on the domain (Google Workspace or an equivalent):
  `hello@`, `security@`, `billing@`, `legal@` — forwarding to one inbox is fine.
- **Legal operator** — no incorporation. `site.ts` `LEGAL_ENTITY` is set to the
  operator's registered persona-física name (the single swap point). Get counsel
  to review the ToS (governing law + liability) and the licensing stack; add a
  DPA + design-partner agreement from a standardized template. Incorporating an
  S.A.S. / S. de R.L. is deferred (RESICO ceiling / customer / legal trigger).
- Verify a **transactional-email sending domain** (Amazon SES — same AWS account,
  same OIDC deploy, no new vendor — or Resend/Postmark if simpler).
- Add a **Clerk custom domain** (`clerk.releasetwin.com`) to the existing
  production Clerk instance.
- Point **Polar** at production (`api.polar.sh`, real product/price ids, the
  persona física / RFC as payee) — Polar (Merchant of Record) remits to the
  registered operator; no incorporated entity required.

**Code / config:**

- Real `SesInvitationEmailSender` (or Resend) implementing the existing
  `IInvitationEmailSender`; bound when a `Notifications:FromAddress` /
  provider config is present, `LoggingInvitationEmailSender` as the fallback so
  local dev and tests are unaffected. **This is the one spec-level change** — the
  `org-membership` invite requirement gains "the invited address receives an
  email".
- Re-point the URL/identity config now scattered as placeholders:
  `NEXT_PUBLIC_SITE_URL` (Vercel), `WEB_BASE_URL` + `Api__PublicUrl` (repo vars),
  the four `POLAR_*_URL` repo vars, `LEGAL_ENTITY` + `LEGAL_CONTACT_EMAIL` in
  `web/src/lib/site.ts`, the `mailto:` links on the security and pricing pages.
- Submit the domain to Google Search Console; leave `/blog` for later.

## Capabilities

### Modified Capabilities

- `org-membership`: the "Admins can invite teammates by email" requirement is
  strengthened — issuing an invitation SHALL send an email to the invited address
  containing the accept link, when a transactional-email provider is configured;
  the accept link is still surfaced in-app as a fallback / for resending.

## Impact

- **hosted API:** one new `IInvitationEmailSender` implementation +
  `AWSSDK.SimpleEmail` (or a Resend HTTP client); DI selection on config; a
  `Notifications:FromAddress` config key. No new endpoint.
- **web/:** `site.ts` constant swaps; `mailto:` link updates; a "resend
  invitation" affordance on the members page (optional, small).
- **terraform / bootstrap:** if SES — an `ses:SendEmail` statement on the API
  Lambda role, scoped to the verified identity; the SES domain identity +
  DKIM records themselves (`hosted/terraform/`).
- **repo variables / Vercel env / Clerk / Polar dashboards:** re-pointed to the
  real domain (enumerated in tasks).
- **docs:** README brand line; `docs/continuity.md` (status/SLA wording is
  handled by `pre-pilot-missing-features` — coordinate); a short
  `docs/company-setup.md` recording what was registered where.
- **no** change to the engine, adapters, or the execution path.

## Manual steps (no code alternative)

These are inherently external actions; the change tracks them but cannot perform
them:

1. Register `releasetwin.com` at a registrar.
2. Purchase / configure the email service and add MX + SPF/DKIM/DMARC records.
3. Confirm the persona-física registered name matches `LEGAL_ENTITY`; engage
   counsel for the ToS + licensing review; draft a short design-partner agreement
   (+ DPA) from a standardized template. (Incorporation is a deferred track.)
4. Add the Clerk custom domain in the Clerk dashboard (DNS CNAME).
5. Switch the Polar account/products to production; payee = the persona física.
6. In each hosting surface (Vercel, GitHub repo variables): set the new values.

## Explicitly deferred

- **Status page + SLA terms** — the actual uptime page (a status subdomain, an
  uptime monitor, published incident history). The copy that references them is
  fixed in `pre-pilot-missing-features`.
- **`/blog`** and ongoing content / SEO.
- **Incorporation** (S.A.S. / S. de R.L. de C.V.) — its own deferred track,
  keyed to the RESICO persona-física revenue ceiling (~3.5M MXN/yr), a customer's
  procurement demand, or legal advice. A registered persona física is enough to
  run a pilot and take Merchant-of-Record payouts.
- D&O insurance, a full negotiated MSA, SOC 2 — not blocking a first pilot.
