# Company setup

Running record of what was registered where, for the `company-and-domain-launch`
change. Fill each section in as the step completes.

## Domain

| Item | Value | Status |
|---|---|---|
| Domain | `releasetwin.com` | **registered 2026-09-01 via Route 53 Domains** |
| Registrar | AWS Route 53 Domains (account `846136340491`) | |
| DNS host | Route 53 hosted zone (auto-created at registration) | Terraform manages records via `data "aws_route53_zone"` in `hosted/terraform/dns-and-email.tf` |

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
| Clerk domain | `classic-marlin-8065.clerk.accounts.dev` | `clerk.releasetwin.com` | |
| `NEXT_PUBLIC_SITE_URL` (Vercel) | Vercel prod URL | `https://releasetwin.com` | |
| `WEB_BASE_URL` repo var | | real domain | |
| `Api__PublicUrl` | `terraform output function_url` (self-heals) | unchanged — verify post-deploy | |
| Google Search Console | | domain submitted | |

## Legal entity

| Item | Value | Status |
|---|---|---|
| Entity type | US LLC | |
| Formation | Stripe Atlas / registered agent | |
| Registered name | _TBD_ → `LEGAL_ENTITY` in `web/src/lib/site.ts` | currently `"the ReleaseTwin project"` |
| EIN | | |
| Contact email | `legal@releasetwin.com` → `LEGAL_CONTACT_EMAIL` | currently `ernestoalejo22@gmail.com` |
| IP assignment | codebase + brand assigned to the LLC | |
| Licensing review | AGPL-3.0 + Adapter Linking Exception + BSL 1.1 | |

## Billing (Polar)

| Item | Value | Status |
|---|---|---|
| Polar mode | sandbox → `api.polar.sh` production | |
| MoR payee | the LLC | |
| Product / price IDs | | |
| `POLAR_UPGRADE_ENABLED` | `false` | flip to `true` after the sandbox e2e passes |
