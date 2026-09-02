Tasks marked **"Needs the user to run this"** are inherently external actions
(registrar, DNS, Stripe Atlas, Polar, Clerk, Vercel, GitHub settings) with no
code path. They are tracked here but cannot be performed from the repo; leave
them unchecked until the user confirms.

## 1. Brand & domain

- [x] 1.1 Trademark glance done 2026-09-02 (IMPI marcanet + USPTO tmsearch + EUIPO eSearch for "ReleaseTwin" / "Release Twin", classes 9/42) — no blocking identical mark. Registration deferred. (dedupe with 6.9)
- [x] 1.2 `releasetwin.com` **registered 2026-09-01** via AWS Route 53 Domains (account `846136340491`); hosted zone auto-created, Terraform manages the records.
- [x] 1.3 Drop the "provisional brand is Validuo" hedge from `README.md` line 5 and any other doc that repeats it (`git grep -i validuo`)
- [x] 1.4 Add `docs/company-setup.md` skeleton (domain, email, entity, DNS records — filled in as steps complete)

## 2. Company email

- [x] 2.1 Google Workspace account created 2026-09-02 (Business Starter, MXN, persona física — RFC via Admin console → Billing, régimen 626/RESICO). Domain **verified**. **Left:** Authenticate email → "Start authentication"; add the four aliases (Directory → Users → alternate emails).
- [x] 2.2 MX + apex SPF + `google._domainkey` DKIM Terraformed in `hosted/terraform/dns-and-email.tf` ("Company mailbox" block), gated on `enable_google_workspace_email`. **All three repo vars set + records live 2026-09-02** (`dig +short TXT google._domainkey.releasetwin.com` returns the split key; MX = `1 smtp.google.com`). DKIM value is split into two 255-char character-strings (PR #102 — the AWS provider does not auto-chunk a >255-byte TXT).
- [x] 2.3 Aliases confirmed 2026-09-02 — `support@` / `security@` / `legal@` / `hello@releasetwin.com` all receive mail sent from an external address. `support` added as the 5th alias.
- [x] 2.4 Record set documented in `docs/company-setup.md` §Company email (Terraform-managed table).

## 3. Transactional email — SES identity (Terraform, CI-only)

- [x] 3.1 Add an SES domain identity + DKIM (`aws_ses_domain_identity`, `aws_ses_domain_dkim`) for the domain in `hosted/terraform/dns-and-email.tf` — gated on the `domain_name` var
- [x] 3.2 Manage the DKIM CNAME + `_amazonses` verification + custom MAIL FROM (MX + SPF) + DMARC records directly as `aws_route53_record` (supersedes "emit as outputs" — the zone is now in Route 53, so Terraform owns the records)
- [x] 3.3 ~~add the SES DNS records at the registrar~~ — **superseded by 3.2**: Route 53 Domains registered the domain, Terraform manages the records in the auto-created hosted zone (looked up via `data "aws_route53_zone"`)
- [x] 3.4 Add a scoped `ses:SendEmail` statement to the API Lambda execution role (`hosted/terraform/lambda.tf`), `Resource` = the SES identity ARN, gated on `domain_name`
- [x] 3.5 SES production access **granted 2026-09-02** — account out of the sandbox, transactional invites can reach any recipient.
- [x] 3.6 CI `terraform apply` **does** need a bootstrap change (the deploy role had no SES/Route 53 perms) — added `SesDomainIdentity` + `Route53Records` statements to `hosted/terraform-bootstrap/main.tf` in the same PR, per that file's documented pattern. Both apply on merge.
- [x] 3.7 `DOMAIN_NAME` = `releasetwin.com` repo var set 2026-09-01. `deploy-hosted.yml` green on the 2026-09-02 run (PR #102). All records resolve: `_amazonses` TXT, 3 SES DKIM CNAMEs, `mail.releasetwin.com` MX/SPF, `_dmarc` (p=none). SES identity + DKIM verified in the console (task A).

## 4. SesInvitationEmailSender (code)

- [x] 4.1 Add `SesInvitationEmailSender : IInvitationEmailSender` in `hosted/ReleaseTwin.Hosted.Api/Services/` — sends a plain-text + HTML invite email with the accept link via the AWS SES SDK
- [x] 4.2 Add `AWSSDK.SimpleEmail` (or `AWSSDK.SimpleEmailV2`) to `ReleaseTwin.Hosted.Api.csproj`
- [x] 4.3 Add a `Notifications:FromAddress` config key (and SES region if not inherited); document it in `docs/` alongside the other hosted config keys
- [x] 4.4 In `Program.cs`, bind `SesInvitationEmailSender` when `Notifications:FromAddress` is present, else keep `LoggingInvitationEmailSender`
- [x] 4.5 Ensure the send is attempted after the invitation row is written and a provider error is caught, logged, and non-fatal (spec: "Email provider failure does not invalidate the invitation")
- [x] 4.6 Unit tests: provider-configured path sends + returns link; no-provider path skips send + returns link; provider-throws path keeps invitation valid + returns link
- [x] 4.7 Update the `IInvitationEmailSender` / `LoggingInvitationEmailSender` XML doc comments (they currently point at "tasks.md 3.8" of a different change)
- [x] 4.8 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count
- [x] 4.9 `NOTIFICATIONS_FROM_ADDRESS` = `no-reply@releasetwin.com` repo var set 2026-09-01; `deploy-hosted.yml` passes it to `notifications_from_address` and Program.cs binds `SesInvitationEmailSender` (identity verified + production per 3.5).
- [ ] 4.10 **Deferred (user's call 2026-09-02)** — issue a real invitation to an external address, confirm the email arrives and the link accepts (evidence-quality: note which inbox, paste the rendered email). Not blocking: `SesInvitationEmailSender` is bound, the SES identity is verified + production, and the Google Workspace sender already passed a mail-tester check (4.11).
- [x] 4.11 Deliverability check done 2026-09-02 (mail-tester) — SPF / DKIM / DMARC all pass from the Google Workspace apex sender, not spam-flagged. SES-path check folded into 4.10.
- [x] 4.12 "Resend invitation" affordance on `/dashboard/members` — `POST /api/organizations/{orgId}/invitations/{token}/resend` (`OrganizationMembersService.ResendInvitationEmailAsync`, admin-gated, re-sends only a still-acceptable invite, token unchanged) + `resendInvitation` server action + "Resend email" button. Tests in `MembershipEndpointsHttpTests` + `OrganizationMembersServiceTests`

## 5. Auth / hosting identity re-pointing

- [x] 5.0 Terraform the Vercel apex A (`216.198.79.1`) + `www` CNAME records in `hosted/terraform/web-dns.tf`, gated on `domain_name` (values from Vercel's DNS-configuration panel 2026-09-01)
- [x] 5.1 Clerk production instance custom domain: 5 CNAMEs (`clerk`, `accounts`, `clkmail`, `clk._domainkey`, `clk2._domainkey`) Terraformed in `hosted/terraform/clerk-dns.tf`, gated on `domain_name`. **User still needs to:** finish the Clerk prod instance setup, grab the `pk_live_`/`sk_live_` keys, click Verify once DNS resolves, then set the Vercel env keys + `CLERK_DOMAIN` repo var (5.1b)
- [x] 5.1b Production Clerk instance deployed + verified 2026-09-01. `pk_live_`/`sk_live_` in Vercel env, `CLERK_DOMAIN` = `clerk.releasetwin.com` repo var applied to the Lambda. Verified: `clerk.releasetwin.com` FAPI + JWKS live, sign-in/sign-up widgets render on `releasetwin.com` as the `production` instance, hosted API rejects bad tokens with 401 (not 500). **Left for the user:** sign up once on the prod pool, then open `/dashboard/me` — it echoes `{ clerkUserId, email, activeOrganizationId, isOperator }` from the resolved session token; confirms the `email` claim is landing (invite acceptance needs it) and gives the `clerkUserId` for `ADMIN_OPERATOR_USER_IDS` (go-public §4.6).
- [x] 5.2 `NEXT_PUBLIC_SITE_URL` = `https://releasetwin.com` set in Vercel; live site metadata now uses the real domain
- [x] 5.3 `WEB_BASE_URL` = `https://releasetwin.com` repo var applied to the Lambda
- [x] 5.4 Verified `Api__PublicUrl` self-heals — the 2026-09-02 deploy read `function_url` from state and passed it back as `-var api_public_url=https://aeq4mvkh3n63sqnngc4lp7567y0mqfzr.lambda-url.us-east-1.on.aws/`; no manual set. Recorded in `docs/company-setup.md`
- [x] 5.5 `releasetwin.com` domain property added to Google Search Console 2026-09-02 — verified off the existing `google-site-verification` apex TXT; sitemap submitted.
- [x] 5.6 Literal sweep: app code already reads `SITE_URL` / `CLERK_DOMAIN` from env — no hard-coded `vercel.app` / `clerk.accounts.dev` in `web/` app code. Contact emails routed through `web/src/lib/site.ts` constants. All `@gmail` / `vercel.app` stale refs swapped 2026-09-02 (PR #104, task 6.5).

## 6. Legal operator

Revised 2026-09-02: the operator is a Mexican tax resident, bootstrapping (no VC),
already registered as **persona física con actividad empresarial (RESICO)** — a
real legal operator. **No US LLC.** Incorporation (S.A.S. / S. de R.L. de C.V.)
is a deferred track keyed to the RESICO revenue ceiling (~3.5M MXN/yr), a
customer's procurement demand, or legal advice — see `deferred` below.

- [x] 6.3 `LEGAL_ENTITY` in `web/src/lib/site.ts` set to the operator's registered persona-física name (`"Ernesto Alejo (persona física con actividad empresarial)"`). `LEGAL_CONTACT_EMAIL` still the gmail until `legal@releasetwin.com` works (6.5).
- [x] 6.3b Confirmed 2026-09-02 — the `LEGAL_ENTITY` string matches the SAT nombre + RFC.
- [x] 6.4 Security + pricing pages reference `SECURITY_CONTACT_EMAIL` / `CONTACT_EMAIL` from `site.ts` instead of a hard-coded `mailto:`.
- [x] 6.5 `git grep -i ernestoalejo22@gmail.com` returns only self-referential planning docs (this change's `proposal.md` / `tasks.md`). Swapped: `web/src/lib/site.ts` (`CONTACT_EMAIL`/`SECURITY_CONTACT_EMAIL`/`LEGAL_CONTACT_EMAIL` → `support@`/`security@`/`legal@releasetwin.com`), root `SECURITY.md`, `SUPPORT.md`, `docs/support.md`, and the `mailto:` links in `.github/ISSUE_TEMPLATE/config.yml` (TODO comments removed). `REUSE.toml` SPDX supplier → `hello@releasetwin.com`. `docs/billing-sandbox-runbook.md` Polar URLs → `https://releasetwin.com`.
- [x] 6.7 Counsel review complete 2026-09-02 — (a) ToS governing-law / dispute clause + liability wording, (b) the AGPL-3.0 + Adapter Linking Exception + BSL 1.1 licensing stack, (c) the DPA + pilot-agreement drafts all reviewed.
- [x] 6.7a DPA + pilot-agreement **drafts** produced from the Common Paper / Bonterms structure, filled with ReleaseTwin facts (MX persona física operator; metadata-only default; AWS/Clerk/Polar/SES subprocessors; per-project evidence retention/purge). `docs/legal/{dpa,pilot-agreement,README}.md`. Marked DRAFT — counsel review (6.7) still required; placeholders (operator RFC/address, governing-law/venue, liability cap, EU SCC member state) flagged in `docs/legal/README.md`.
- [x] 6.8 `next build` + `npx eslint` green after the `site.ts` / page edits (re-run after the 6.5 email swap).
- [x] 6.9 Done — see 1.1 (trademark glance 2026-09-02, no blocking mark, registration deferred).

### Deferred — incorporation track

- [ ] 6.D1 **Deferred** — form a Mexican entity (**S.A.S.** — free, online at tuempresa.gob.mx, single shareholder, limited liability, ~5M MXN cap — or **S. de R.L. de C.V.**). Trigger: nearing the RESICO persona-física ceiling, a customer procurement requirement, or legal advice. Ask a contador + tech lawyer which form fits.
- [ ] 6.D2 **Deferred** — on incorporation: assign the codebase + brand IP to the entity; re-point `LEGAL_ENTITY` + the Polar payee; update the SPDX copyright line.

## 7. Billing → production (Polar)

- [ ] 7.1 **Needs the user to run this** — run the Polar sandbox e2e (`docs/billing-sandbox-runbook.md`); confirm a full upgrade + webhook round-trip
- [ ] 7.2 **Needs the user to run this** — switch the Polar account to production (`api.polar.sh`), create real product/price IDs; Merchant-of-Record payee = the persona física (RFC + CLABE / bank account). Confirm Polar supports individual/sole-proprietor payouts for Mexico.
- [ ] 7.3 **Needs the user to run this** — set the four `POLAR_*_URL` repo vars + real product/price IDs
- [ ] 7.4 **Needs the user to run this** — set `POLAR_UPGRADE_ENABLED=true` and take reconciliation out of dry-run (only after 7.1 passes)
- [x] 7.5 Documented in `docs/company-setup.md` ("Transport security"): the ingest API is served by an AWS Lambda Function URL, which is HTTPS-only (AWS terminates TLS, no HTTP listener) — plus `UseHttpsRedirection()` + HSTS. No plaintext endpoint anywhere

## 8. Close-out

- [x] 8.1 `docs/company-setup.md` complete 2026-09-02 — domain + all Terraform-managed DNS records (web / Clerk / SES / Google Workspace), company email, SES transactional (verified + production), legal operator, transport security, and a "Where each thing is administered" table (AWS/Route 53/SES, Google Admin, Vercel, Clerk, Polar, Search Console, GitHub repo vars, IAM/OIDC)
- [x] 8.2 `git grep -i "validuo\|provisional brand\|working name"` is clean (handled in 1.3); root `README.md` brand line is the real tagline. The remaining "solo-maintained" phrasing in CONTRIBUTING/SECURITY/SUPPORT is deliberate and accurate, not provisional-entity copy
- [x] 8.3 `openspec validate company-and-domain-launch --strict` passes (2026-09-02)
- [ ] 8.4 Confirm with the user before archiving
