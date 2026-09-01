Tasks marked **"Needs the user to run this"** are inherently external actions
(registrar, DNS, Stripe Atlas, Polar, Clerk, Vercel, GitHub settings) with no
code path. They are tracked here but cannot be performed from the repo; leave
them unchecked until the user confirms.

## 1. Brand & domain

- [ ] 1.1 **Needs the user to run this** — quick USPTO/EUIPO trademark glance for "ReleaseTwin"
- [ ] 1.2 **Needs the user to run this** — register `releasetwin.com` at a registrar
- [x] 1.3 Drop the "provisional brand is Validuo" hedge from `README.md` line 5 and any other doc that repeats it (`git grep -i validuo`)
- [x] 1.4 Add `docs/company-setup.md` skeleton (domain, email, entity, DNS records — filled in as steps complete)

## 2. Company email

- [ ] 2.1 **Needs the user to run this** — create Google Workspace (or equivalent) on `releasetwin.com`; `hello@`, `security@`, `billing@`, `legal@` forwarding to one inbox
- [ ] 2.2 **Needs the user to run this** — add MX + SPF + DKIM + DMARC DNS records for the mailbox provider
- [ ] 2.3 Verify mail flow: send to each alias from an external address, confirm it lands in the shared inbox
- [ ] 2.4 Record the final DNS record set in `docs/company-setup.md`

## 3. Transactional email — SES identity (Terraform, CI-only)

- [x] 3.1 Add an SES domain identity + DKIM (`aws_ses_domain_identity`, `aws_ses_domain_dkim`) for the domain in `hosted/terraform/dns-and-email.tf` — gated on the `domain_name` var
- [x] 3.2 Manage the DKIM CNAME + `_amazonses` verification + custom MAIL FROM (MX + SPF) + DMARC records directly as `aws_route53_record` (supersedes "emit as outputs" — the zone is now in Route 53, so Terraform owns the records)
- [x] 3.3 ~~add the SES DNS records at the registrar~~ — **superseded by 3.2**: Route 53 Domains registered the domain, Terraform manages the records in the auto-created hosted zone (looked up via `data "aws_route53_zone"`)
- [x] 3.4 Add a scoped `ses:SendEmail` statement to the API Lambda execution role (`hosted/terraform/lambda.tf`), `Resource` = the SES identity ARN, gated on `domain_name`
- [ ] 3.5 **Needs the user to run this** — request SES production access (move out of sandbox) once the identity verifies
- [x] 3.6 CI `terraform apply` **does** need a bootstrap change (the deploy role had no SES/Route 53 perms) — added `SesDomainIdentity` + `Route53Records` statements to `hosted/terraform-bootstrap/main.tf` in the same PR, per that file's documented pattern. Both apply on merge.
- [ ] 3.7 **Needs the user to run this** — set the `DOMAIN_NAME` repo var to `releasetwin.com` to activate the SES/DNS resources, then confirm `bootstrap.yml` + `deploy-hosted.yml` both go green (re-run deploy if it raced bootstrap on the first merge)

## 4. SesInvitationEmailSender (code)

- [x] 4.1 Add `SesInvitationEmailSender : IInvitationEmailSender` in `hosted/ReleaseTwin.Hosted.Api/Services/` — sends a plain-text + HTML invite email with the accept link via the AWS SES SDK
- [x] 4.2 Add `AWSSDK.SimpleEmail` (or `AWSSDK.SimpleEmailV2`) to `ReleaseTwin.Hosted.Api.csproj`
- [x] 4.3 Add a `Notifications:FromAddress` config key (and SES region if not inherited); document it in `docs/` alongside the other hosted config keys
- [x] 4.4 In `Program.cs`, bind `SesInvitationEmailSender` when `Notifications:FromAddress` is present, else keep `LoggingInvitationEmailSender`
- [x] 4.5 Ensure the send is attempted after the invitation row is written and a provider error is caught, logged, and non-fatal (spec: "Email provider failure does not invalidate the invitation")
- [x] 4.6 Unit tests: provider-configured path sends + returns link; no-provider path skips send + returns link; provider-throws path keeps invitation valid + returns link
- [x] 4.7 Update the `IInvitationEmailSender` / `LoggingInvitationEmailSender` XML doc comments (they currently point at "tasks.md 3.8" of a different change)
- [x] 4.8 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count
- [ ] 4.9 **Needs the user to run this** — set the `NOTIFICATIONS_FROM_ADDRESS` repo var (e.g. `no-reply@releasetwin.com`) once the SES identity from 3.x is verified; `deploy-hosted.yml` passes it to `notifications_from_address` and Program.cs binds `SesInvitationEmailSender`
- [ ] 4.10 Post-deploy: issue a real invitation to an external address, confirm the email arrives and the link accepts (evidence-quality: note which inbox, paste the rendered email)
- [ ] 4.11 Deliverability check (mail-tester or equivalent) — SPF/DKIM/DMARC all pass, not flagged as spam
- [ ] 4.12 (stretch, optional) "Resend invitation" affordance on `/dashboard/members`

## 5. Auth / hosting identity re-pointing

- [x] 5.0 Terraform the Vercel apex A (`216.198.79.1`) + `www` CNAME records in `hosted/terraform/web-dns.tf`, gated on `domain_name` (values from Vercel's DNS-configuration panel 2026-09-01)
- [x] 5.1 Clerk production instance custom domain: 5 CNAMEs (`clerk`, `accounts`, `clkmail`, `clk._domainkey`, `clk2._domainkey`) Terraformed in `hosted/terraform/clerk-dns.tf`, gated on `domain_name`. **User still needs to:** finish the Clerk prod instance setup, grab the `pk_live_`/`sk_live_` keys, click Verify once DNS resolves, then set the Vercel env keys + `CLERK_DOMAIN` repo var (5.1b)
- [ ] 5.1b **Needs the user to run this** — production Clerk keys into Vercel env (`NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` = `pk_live_…`, `CLERK_SECRET_KEY` = `sk_live_…`, Production scope) + set `CLERK_DOMAIN` repo var to `clerk.releasetwin.com`; redeploy web + hosted API. Re-create the operator's own login on the prod instance (fresh user pool).
- [ ] 5.2 **Needs the user to run this** — after DNS resolves and the site loads on `releasetwin.com`, set `NEXT_PUBLIC_SITE_URL` = `https://releasetwin.com` in **Vercel** → Settings → Environment Variables (Production), then redeploy
- [ ] 5.3 **Needs the user to run this** — set the `WEB_BASE_URL` repo var (Actions → Variables) to `https://releasetwin.com` once 5.2 is live — it builds the invite accept link + notification deep links, so it must point at a domain that serves
- [ ] 5.4 Verify `Api__PublicUrl` self-heals from `terraform output function_url` (no manual set needed) — confirm the value post-deploy
- [ ] 5.5 **Needs the user to run this** — submit `releasetwin.com` to Google Search Console
- [x] 5.6 Literal sweep: app code already reads `SITE_URL` / `CLERK_DOMAIN` from env — no hard-coded `vercel.app` / `clerk.accounts.dev` in `web/` app code. Contact emails routed through `web/src/lib/site.ts` constants. Remaining stale refs are `docs/billing-sandbox-runbook.md` + root `SECURITY.md` (both wait on the domain addresses existing)

## 6. Legal entity

- [ ] 6.1 **Needs the user to run this** — form the US LLC (Stripe Atlas or registered agent)
- [ ] 6.2 **Needs the user to run this** — obtain the EIN; record the registered legal name
- [ ] 6.3 Set `LEGAL_ENTITY` and `LEGAL_CONTACT_EMAIL` in `web/src/lib/site.ts` to the LLC name and `legal@releasetwin.com`
- [x] 6.4 Security + pricing pages now reference `SECURITY_CONTACT_EMAIL` / `CONTACT_EMAIL` from `web/src/lib/site.ts` instead of a hard-coded `mailto:` — values still the gmail until 6.3
- [ ] 6.5 `git grep -i ernestoalejo22@gmail.com` in `web/` returns only `site.ts` (the single swap point); root `SECURITY.md` still to update when `security@releasetwin.com` works
- [ ] 6.6 **Needs the user to run this** — IP ownership doc: assign the codebase/brand to the LLC
- [ ] 6.7 **Needs the user to run this** — legal review of the AGPL-3.0 + Adapter Linking Exception + BSL 1.1 stack
- [ ] 6.8 `next build` + `npx eslint` green after the `site.ts` / page edits

## 7. Billing → production (Polar)

- [ ] 7.1 **Needs the user to run this** — run the Polar sandbox e2e (`docs/billing-sandbox-runbook.md`); confirm a full upgrade + webhook round-trip
- [ ] 7.2 **Needs the user to run this** — switch the Polar account to production (`api.polar.sh`), create real product/price IDs, set the LLC as Merchant-of-Record payee
- [ ] 7.3 **Needs the user to run this** — set the four `POLAR_*_URL` repo vars + real product/price IDs
- [ ] 7.4 **Needs the user to run this** — set `POLAR_UPGRADE_ENABLED=true` and take reconciliation out of dry-run (only after 7.1 passes)
- [ ] 7.5 Confirm TLS is terminated on the evidence-ingest API endpoint (or document why the Function URL already covers it)

## 8. Close-out

- [ ] 8.1 `docs/company-setup.md` complete — domain, email, entity, all DNS records, dashboard locations
- [ ] 8.2 README brand line + any stale "no entity / provisional" copy updated
- [ ] 8.3 `openspec validate company-and-domain-launch --strict` passes
- [ ] 8.4 Confirm with the user before archiving
