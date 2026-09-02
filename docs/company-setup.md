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
| Company mailbox (Google Workspace) | `dns-and-email.tf` | apex `MX` → `1 smtp.google.com`; apex SPF + `google-site-verification` `TXT`; `google._domainkey` DKIM `TXT`; shares the one `_dmarc` `TXT` — see "Company email" below |

## Company email

**Provider: Google Workspace** (chosen 2026-09-02). One `Business Starter` seat;
`hello@`, `support@`, `security@`, `billing@`, `legal@` are alternate-email
aliases on that seat, all landing in the one inbox.

DNS is **Terraform-managed** in `hosted/terraform/dns-and-email.tf` on the apex,
gated on `enable_google_workspace_email` — separate from the SES records, which
send transactional mail from the `mail.releasetwin.com` subdomain. The two don't
collide: apex SPF authorises Google, the MAIL FROM subdomain SPF authorises SES,
one `_dmarc` record (already at `p=none`) covers both.

| Record | Type | Value | Status |
|---|---|---|---|
| apex `MX` | MX | `1 smtp.google.com` | **live 2026-09-02** (Terraform `gws_mx`) |
| apex `TXT` | TXT | `v=spf1 include:_spf.google.com ~all` + `google-site-verification=…` | **live 2026-09-02** (Terraform `gws_apex_txt`; `ENABLE_GOOGLE_WORKSPACE_EMAIL` + `GOOGLE_SITE_VERIFICATION` repo vars set) |
| `google._domainkey` | TXT | the 2048-bit DKIM key, stored as two 255-char character-strings | **live + authenticated 2026-09-02** (Terraform `gws_dkim`; `GOOGLE_WORKSPACE_DKIM` repo var set; "Start authentication" done in Admin console) |
| `_dmarc` | TXT | `v=DMARC1; p=none; rua=mailto:security@releasetwin.com` | live (from the SES work) |

**Status 2026-09-02 — complete:** account created (`Business Starter`, MXN
billing, persona física — RFC entered in Admin console → Billing, not the signup
wizard, régimen 626/RESICO); domain verified; all DNS live via Terraform; DKIM
authenticated; the five aliases added; each alias confirmed to receive external
mail (task 2.3); mail-tester run clean — SPF + DKIM + DMARC all pass (task 4.11).

**Optional follow-up:** once the SES path (task 4.10) has also produced clean
DMARC aggregate reports, tighten `_dmarc` to `p=quarantine` in `dns-and-email.tf`.

`company-and-domain-launch` task 6.5 is **done** — `SECURITY.md`, `SUPPORT.md`,
`docs/support.md`, `.github/ISSUE_TEMPLATE/config.yml`, and the `web/src/lib/site.ts`
contact constants now use the `@releasetwin.com` addresses.

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

| Item | Value | Status |
|---|---|---|
| `DOMAIN_NAME` repo var | `releasetwin.com` | **set 2026-09-01** — SES/DNS resources active |
| `NOTIFICATIONS_FROM_ADDRESS` repo var | `no-reply@releasetwin.com` | **set 2026-09-01** — `SesInvitationEmailSender` bound |
| SES region | inherits `Aws:Region` (`us-east-1`) | |
| SES identity verification | `releasetwin.com` | **Verified 2026-09-02** (DKIM Successful) |
| SES sandbox | | **production access granted 2026-09-02** |
| Custom MAIL FROM | `mail.releasetwin.com` | **live 2026-09-02** (MX + SPF resolve) |

Remaining: **task 4.10 (deferred)** — issue a real invitation to an external
address and paste the rendered email + inbox into the task notes. Not a blocker:
the sender is bound, the identity is production, and the Google-Workspace path
already passed a deliverability check.

## Auth / hosting identity

| Item | Current | Target | Status |
|---|---|---|---|
| Clerk domain | `classic-marlin-8065.clerk.accounts.dev` | `clerk.releasetwin.com` | **prod instance verified 2026-09-01**; `CLERK_DOMAIN` repo var = `clerk.releasetwin.com` |
| `NEXT_PUBLIC_SITE_URL` (Vercel) | Vercel prod URL | `https://releasetwin.com` | **set 2026-09-01** |
| `WEB_BASE_URL` repo var | | `https://releasetwin.com` | **set 2026-09-01** |
| `Api__PublicUrl` | `terraform output function_url` (self-heals) | unchanged | **confirmed 2026-09-02** — deploy reads `function_url` from state and feeds it back as `-var`; live value `https://aeq4mvkh3n63sqnngc4lp7567y0mqfzr.lambda-url.us-east-1.on.aws/` (will change once a custom API domain is set, still self-heals) |
| Google Search Console | | `releasetwin.com` domain property | **submitted + verified 2026-09-02** (verified off the existing `google-site-verification` apex TXT) |

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
| `LEGAL_ENTITY` (`web/src/lib/site.ts`) | `"Ernesto Alejo (persona física con actividad empresarial)"` | **confirmed 2026-09-02** — matches the SAT nombre + RFC |
| Operator account / `ADMIN_OPERATOR_USER_IDS` | operator's Clerk **prod** user id → repo var | **deferred** — no operator-only endpoint is needed for a free pilot; set it (via `/dashboard/me`) when the Enterprise-grant / operator console work lands |
| Contact email | `legal@releasetwin.com` → `LEGAL_CONTACT_EMAIL` | done — `support@` / `security@` / `legal@` aliases on the mailbox |
| ToS counsel review | governing-law/dispute clause (Mexican law default) + liability wording — `web/src/app/(marketing)/terms/page.tsx` | **reviewed 2026-09-02** |
| Licensing review | AGPL-3.0 + Adapter Linking Exception + BSL 1.1 | **reviewed 2026-09-02** (same engagement) |
| DPA + design-partner agreement | `docs/legal/dpa.md` + `docs/legal/pilot-agreement.md` — drafted from the Common Paper / Bonterms structure with ReleaseTwin facts | **reviewed 2026-09-02** |
| Incorporation | S.A.S. or S. de R.L. de C.V. | **deferred** |
| IP assignment | codebase + brand → the entity | deferred (on incorporation) |
| Polar MoR payee | the persona física (RFC + bank/CLABE) | pending Polar production (§7) |

## Where each thing is administered

| Surface | Console | Account / identifier |
|---|---|---|
| Domain + DNS | AWS Route 53 → Hosted zones → `releasetwin.com` | AWS account `846136340491`, region `us-east-1` |
| Domain registration | AWS Route 53 → Registered domains | same account; auto-renew on |
| Transactional email | AWS SES → `us-east-1` → Identities → `releasetwin.com` | same account; production access granted |
| Company mailbox | [admin.google.com](https://admin.google.com) | Google Workspace, one `Business Starter` seat, MXN billing |
| DKIM key generation | Google Admin → Apps → Google Workspace → Gmail → Authenticate email | selector `google`, 2048-bit |
| Web hosting | [vercel.com](https://vercel.com) → `releasetwin` project | env: `NEXT_PUBLIC_SITE_URL`, `pk_live_` / `sk_live_` Clerk keys |
| Auth | [dashboard.clerk.com](https://dashboard.clerk.com) → production instance | custom domain `clerk.releasetwin.com` |
| Billing | [polar.sh](https://polar.sh) | sandbox today; production pending §7 |
| Search Console | [search.google.com/search-console](https://search.google.com/search-console) | `releasetwin.com` domain property |
| Infra config | GitHub repo → Settings → Secrets and variables → Actions | all `*_URL` / `DOMAIN_NAME` / `CLERK_*` / `POLAR_*` repo vars; Terraform applies them via `deploy-hosted.yml` |
| IAM / OIDC | AWS IAM → Roles → `releasetwin-github-actions-deploy` / `-e2e` | trust = the GitHub OIDC provider |

## Billing (Polar)

| Item | Value | Status |
|---|---|---|
| Polar mode | sandbox → `api.polar.sh` production | |
| MoR payee | the persona física (RFC + bank/CLABE) | |
| Product / price IDs | | |
| `POLAR_UPGRADE_ENABLED` | `false` | flip to `true` after the sandbox e2e passes |
