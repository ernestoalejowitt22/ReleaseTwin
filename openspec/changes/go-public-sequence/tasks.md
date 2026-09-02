Most tasks here are **"Needs the user to run this"** — repo settings, a GitHub
Support ticket, vendor dashboards. They are tracked but not performable from the
repo. This change is downstream of `company-and-domain-launch` (domain, the ToS +
licensing counsel review, billing to production) and does not block a first paid
pilot. It does NOT wait on incorporation — the operator runs as a registered
persona física.

## 1. Prod-stack decision

- [x] 1.1 Confirmed — `alerting.tf` (5xx/error/throttle alarms + SNS + staleness digest), `evidence.tf` (blob bucket + scheduled purge), `notifications.tf`, `billing.tf` all apply with the `releasetwin-dev-` prefix. Table in `docs/go-public-runbook.md` §1.
- [x] 1.2 `docs/go-public-runbook.md` §1: pilot on the `releasetwin-dev-` stack (prefix is cosmetic, no data bleed — local dev uses DynamoDB Local); cut a real prod stack at customer #2 or a compliance ask.
- [x] 1.3 `docs/go-public-runbook.md` §1.1 — 7-step migration (workflow_dispatch new prefix, DynamoDB export/import, `s3 sync` the evidence bucket, `Api__PublicUrl` self-heals, bootstrap IAM prefix update, soak, destroy old) + enable PITR on the cut.

## 2. Repo visibility

The history-cache concern is closed — NOT PURSUED (decision 2026-09-01): both
repos private, 0 forks, no fork network, no pre-rewrite external clone; residual
risk accepted (`docs/go-public-runbook.md` §2).

- [ ] 2.1 **Needs the user to run this** — polish `ReleaseTwin` repo description + topics; confirm `SECURITY.md` contact is on the company domain
- [ ] 2.2 Pre-flip sanity: fresh clone of the about-to-be-public repo, `git rev-list --all | xargs -I{} git grep -i <prior-vendor-term> {}` returns zero on reachable history
- [x] 2.3 `company-and-domain-launch` §6.7 counsel review complete 2026-09-02 (ToS governing-law/liability + AGPL/BSL stack + DPA/pilot-agreement drafts). Note: `docs/legal/README.md` placeholders (operator RFC/address, governing-law/venue, liability cap) still need filling before a real pilot signs.
- [ ] 2.4 **Needs the user to run this** — flip `ReleaseTwin` to public (only after 2.2 + 2.3)
- [ ] 2.5 Decide whether `NAHA` goes public or stays private as the demo target (design Open Question); flip only if decided yes
- [ ] 2.6 Post-flip: confirm the Vercel Preview demo target still builds and renders

## 4. Open self-serve sign-up

- [ ] 4.1 Verify `company-and-domain-launch` §7 (Polar in production, `POLAR_UPGRADE_ENABLED=true`) is complete
- [ ] 4.2 Walk the funnel end to end on the live site: sign-up → org provisioned → first project created → entitlements reflect Free tier
- [x] 4.3 `site-header.tsx` gains a primary "Sign up" button (logged-out); homepage hero "Sign in to get started" → "Get started free" → `/sign-up` (+ copy); pricing Free tier + `docs/hosted-platform` → `/sign-up`.
- [x] 4.4 `next build` compiled + `npx eslint` clean on the 4 changed files.
- [ ] 4.5 **Needs the user to run this** — deploy; do one real external sign-up + upgrade-to-Team round trip; confirm the webhook and entitlement change land
- [ ] 4.6 **Needs the user to run this** — `ADMIN_OPERATOR_USER_IDS` repo var is **NOT set** (checked 2026-09-01). After signing up on prod Clerk, open `/dashboard/me` for the `clerkUserId`, then `gh variable set ADMIN_OPERATOR_USER_IDS -b '<clerkUserId>'`; redeploy; reload `/dashboard/me` and confirm `isOperator: true`. Empty ⇒ admin surface closed (safe).

## 5. Close-out

- [x] 5.1 `docs/go-public-runbook.md` created — prod-stack decision + migration + per-section status. Updated as sections complete.
- [x] 5.2 `openspec validate go-public-sequence --strict` passes (2026-09-02)
- [ ] 5.3 Confirm with the user before archiving
