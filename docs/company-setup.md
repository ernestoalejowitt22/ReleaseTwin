# Company setup

Running record of what was registered where, for the `company-and-domain-launch`
change. Fill each section in as the step completes.

## Domain

| Item | Value | Status |
|---|---|---|
| Domain | `releasetwin.com` | **registered 2026-09-01 via Route 53 Domains** |
| Registrar | AWS Route 53 Domains (account `846136340491`) | |
| DNS host | Route 53 hosted zone (auto-created at registration) | Terraform manages records via `data "aws_route53_zone"` in `hosted/terraform/dns-and-email.tf` |

### DNS records (Terraform-managed)

All in `hosted/terraform/*.tf`, gated on the `domain_name` var, applied by
`deploy-hosted.yml`. Nothing is entered at a registrar by hand.

| Purpose | File | Records |
|---|---|---|
| Web (Vercel) | `web-dns.tf` | apex `A` → `216.198.79.1`; `www` `CNAME` → Vercel |
| Auth (Clerk prod) | `clerk-dns.tf` | 5 `CNAME`: `clerk`, `accounts`, `clkmail`, `clk._domainkey`, `clk2._domainkey` |
| Transactional email (SES) | `dns-and-email.tf` | 3 DKIM `CNAME`; `_amazonses` verification `TXT`; MAIL FROM `mail.releasetwin.com` `MX` + SPF `TXT`; `_dmarc` `TXT` (`p=none`) |
| Company mailbox | _pending_ | MX / SPF / DKIM / DMARC for the mailbox provider — see "Company email" below |

## Company email

Provider: _TBD_ (Google Workspace or equivalent). Aliases `hello@`, `security@`,
`billing@`, `legal@` forwarding to one inbox.

| Record | Type | Value | Status |
|---|---|---|---|
| MX | | | |
| SPF | TXT | | |
| DKIM | CNAME/TXT | | |
| DMARC | TXT | | |

## Transactional email (SES)

Used by `SesInvitationEmailSender` to deliver org invitations. The sender is
already in the codebase; it is bound only when `Notifications:FromAddress` is
set, otherwise `LoggingInvitationEmailSender` runs and the accept link is
returned in the invite API response.

Wired in Terraform (`hosted/terraform/dns-and-email.tf` + `lambda.tf` +
`terraform-bootstrap/main.tf`), all gated on the `domain_name` var:

- `aws_ses_domain_identity` + `aws_ses_domain_dkim` + `aws_ses_domain_mail_from` for the domain.
- `aws_route53_record` for the 3 DKIM CNAMEs, `_amazonses` verification TXT, MAIL FROM MX + SPF, and a `p=none` DMARC record — managed directly, no manual DNS entry.
- Scoped `ses:SendEmail` statement on `aws_iam_role.hosted_api`, `Resource` = the identity ARN.
- `notifications_from_address` var → `Notifications__FromAddress` Lambda env var (empty-default pattern).
- Deploy-role permissions (`SesDomainIdentity`, `Route53Records`) added to bootstrap.

Remaining user steps:

1. Set repo var `DOMAIN_NAME` = `releasetwin.com` → triggers `deploy-hosted.yml`, which creates the identity + DNS records. Confirm `bootstrap.yml` ran first (re-run deploy if it raced).
2. Wait for SES to verify the identity (async, off the DNS records — minutes to a few hours).
3. Request **SES production access** (out of sandbox) — until then, only verified recipient addresses receive mail.
4. Set repo var `NOTIFICATIONS_FROM_ADDRESS` = `no-reply@releasetwin.com` → binds `SesInvitationEmailSender`.
5. Issue a real invitation to an external address; run a deliverability check (task 4.10, 4.11).

| Item | Value | Status |
|---|---|---|
| `DOMAIN_NAME` repo var | `releasetwin.com` | not set — SES/DNS resources dormant |
| `NOTIFICATIONS_FROM_ADDRESS` repo var | e.g. `no-reply@releasetwin.com` | not set — logging fallback active |
| SES region | inherits `Aws:Region` (`us-east-1`) | |
| SES identity verification | | pending `DOMAIN_NAME` |
| SES sandbox | in sandbox | production-access request pending |
| Custom MAIL FROM | `mail.releasetwin.com` | pending `DOMAIN_NAME` |

## Auth / hosting identity

| Item | Current | Target | Status |
|---|---|---|---|
| Clerk domain | `classic-marlin-8065.clerk.accounts.dev` | `clerk.releasetwin.com` | **prod instance verified 2026-09-01**; `CLERK_DOMAIN` repo var = `clerk.releasetwin.com` |
| `NEXT_PUBLIC_SITE_URL` (Vercel) | Vercel prod URL | `https://releasetwin.com` | **set 2026-09-01** |
| `WEB_BASE_URL` repo var | | `https://releasetwin.com` | **set 2026-09-01** |
| `Api__PublicUrl` | `terraform output function_url` (self-heals) | unchanged | **confirmed 2026-09-02** — deploy reads `function_url` from state and feeds it back as `-var`; live value `https://aeq4mvkh3n63sqnngc4lp7567y0mqfzr.lambda-url.us-east-1.on.aws/` (will change once a custom API domain is set, still self-heals) |
| Google Search Console | | domain submitted | pending user |

## Transport security

Every hosted surface is HTTPS-only, TLS terminated by the platform:

- **Hosted API + evidence ingest** — served by an AWS Lambda **Function URL**
  (`aws_lambda_function_url.hosted_api`). Function URLs accept HTTPS only; there
  is no HTTP listener to downgrade to, and AWS owns the TLS termination and
  certificate. The app additionally runs `UseHttpsRedirection()` + HSTS
  (`Program.cs`), so any proxied `http` is 308'd and browsers pin HTTPS.
- **Web** — Vercel terminates TLS for `releasetwin.com` (managed cert).
- **Auth** — Clerk terminates TLS for `clerk.releasetwin.com` (managed cert via
  the Terraformed CNAMEs).

No plaintext endpoint is exposed anywhere in the deployment.

## Legal operator

Revised 2026-09-02 — see `openspec/changes/company-and-domain-launch/design.md`
("Legal operator"). Operator is a **Mexican tax resident, bootstrapping**,
**already registered as persona física con actividad empresarial under RESICO**.
No US LLC (no VC forcing function, no US-procurement requirement, and it would add
a US Form 5472 filing burden). Incorporation (S.A.S. / S. de R.L. de C.V.) is a
deferred track — trigger: RESICO ceiling (~3.5M MXN/yr) / customer demand / legal
advice.

| Item | Value | Status |
|---|---|---|
| Legal form | persona física con actividad empresarial (RESICO) | **already registered** |
| `LEGAL_ENTITY` (`web/src/lib/site.ts`) | `"Ernesto Alejo (persona física con actividad empresarial)"` | **set 2026-09-02** — user to confirm it matches the SAT nombre + RFC |
| Contact email | `legal@releasetwin.com` → `LEGAL_CONTACT_EMAIL` | currently `ernestoalejo22@gmail.com` (swaps with the mailbox) |
| ToS counsel review | governing-law/dispute clause (Mexican law default) + liability wording — `web/src/app/(marketing)/terms/page.tsx` | **pending** — pre-GA gate, not a pilot blocker |
| Licensing review | AGPL-3.0 + Adapter Linking Exception + BSL 1.1 | **pending** (same counsel engagement) |
| DPA + design-partner agreement | `docs/legal/dpa.md` + `docs/legal/pilot-agreement.md` — drafted from the Common Paper / Bonterms structure with ReleaseTwin facts | **drafts ready 2026-09-02; counsel review pending** |
| Incorporation | S.A.S. or S. de R.L. de C.V. | **deferred** |
| IP assignment | codebase + brand → the entity | deferred (on incorporation) |
| Polar MoR payee | the persona física (RFC + bank/CLABE) | pending Polar production (§7) |

## Billing (Polar)

| Item | Value | Status |
|---|---|---|
| Polar mode | sandbox → `api.polar.sh` production | |
| MoR payee | the persona física (RFC + bank/CLABE) | |
| Product / price IDs | | |
| `POLAR_UPGRADE_ENABLED` | `false` | flip to `true` after the sandbox e2e passes |
