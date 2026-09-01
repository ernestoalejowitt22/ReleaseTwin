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

- [ ] 3.1 Add an SES domain identity + DKIM (`aws_ses_domain_identity`, `aws_ses_domain_dkim`) for `releasetwin.com` in `hosted/terraform/`
- [ ] 3.2 Emit the DKIM CNAME + `MAIL FROM` records as Terraform outputs
- [ ] 3.3 **Needs the user to run this** — add the SES DKIM / MAIL FROM DNS records at the registrar
- [ ] 3.4 Add a scoped `ses:SendEmail` / `ses:SendRawEmail` statement to the API Lambda execution role, `Resource` = the verified identity ARN
- [ ] 3.5 **Needs the user to run this** — request SES production access (move out of sandbox)
- [ ] 3.6 Confirm the CI `terraform apply` on merge provisions the identity and role statement without a bootstrap change

## 4. SesInvitationEmailSender (code)

- [x] 4.1 Add `SesInvitationEmailSender : IInvitationEmailSender` in `hosted/ReleaseTwin.Hosted.Api/Services/` — sends a plain-text + HTML invite email with the accept link via the AWS SES SDK
- [x] 4.2 Add `AWSSDK.SimpleEmail` (or `AWSSDK.SimpleEmailV2`) to `ReleaseTwin.Hosted.Api.csproj`
- [x] 4.3 Add a `Notifications:FromAddress` config key (and SES region if not inherited); document it in `docs/` alongside the other hosted config keys
- [x] 4.4 In `Program.cs`, bind `SesInvitationEmailSender` when `Notifications:FromAddress` is present, else keep `LoggingInvitationEmailSender`
- [x] 4.5 Ensure the send is attempted after the invitation row is written and a provider error is caught, logged, and non-fatal (spec: "Email provider failure does not invalidate the invitation")
- [x] 4.6 Unit tests: provider-configured path sends + returns link; no-provider path skips send + returns link; provider-throws path keeps invitation valid + returns link
- [x] 4.7 Update the `IInvitationEmailSender` / `LoggingInvitationEmailSender` XML doc comments (they currently point at "tasks.md 3.8" of a different change)
- [x] 4.8 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count
- [ ] 4.9 **Needs the user to run this** — set `Notifications:FromAddress` (repo var / terraform var) once SES identity 3.x is verified
- [ ] 4.10 Post-deploy: issue a real invitation to an external address, confirm the email arrives and the link accepts (evidence-quality: note which inbox, paste the rendered email)
- [ ] 4.11 Deliverability check (mail-tester or equivalent) — SPF/DKIM/DMARC all pass, not flagged as spam
- [ ] 4.12 (stretch, optional) "Resend invitation" affordance on `/dashboard/members`

## 5. Auth / hosting identity re-pointing

- [ ] 5.1 **Needs the user to run this** — add Clerk custom domain `clerk.releasetwin.com` to the production Clerk instance; add the CNAME at the registrar
- [ ] 5.2 **Needs the user to run this** — set `NEXT_PUBLIC_SITE_URL` = `https://releasetwin.com` in Vercel (prod)
- [ ] 5.3 **Needs the user to run this** — set `WEB_BASE_URL` repo var to the real domain
- [ ] 5.4 Verify `Api__PublicUrl` self-heals from `terraform output function_url` (no manual set needed) — confirm the value post-deploy
- [ ] 5.5 **Needs the user to run this** — submit `releasetwin.com` to Google Search Console
- [ ] 5.6 Sweep `git grep` for any remaining `vercel.app` / `clerk.accounts.dev` / `classic-marlin` literals in `web/` and docs; replace with the constant or the real domain

## 6. Legal entity

- [ ] 6.1 **Needs the user to run this** — form the US LLC (Stripe Atlas or registered agent)
- [ ] 6.2 **Needs the user to run this** — obtain the EIN; record the registered legal name
- [ ] 6.3 Set `LEGAL_ENTITY` and `LEGAL_CONTACT_EMAIL` in `web/src/lib/site.ts` to the LLC name and `legal@releasetwin.com`
- [ ] 6.4 Migrate the hard-coded `mailto:ernestoalejo22@gmail.com` links on the security and pricing pages to reference `LEGAL_CONTACT_EMAIL` / `hello@` / `security@`
- [ ] 6.5 `git grep -i ernestoalejo22@gmail.com` in `web/` and public docs returns nothing
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
