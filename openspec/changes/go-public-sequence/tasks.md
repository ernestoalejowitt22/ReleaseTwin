Most tasks here are **"Needs the user to run this"** — repo settings, a GitHub
Support ticket, vendor dashboards. They are tracked but not performable from the
repo. This change is downstream of `company-and-domain-launch` (domain, entity,
billing to production) and does not block a first paid pilot.

## 1. Prod-stack decision

- [ ] 1.1 Confirm the `releasetwin-dev-` auto-deploy stack carries alerting, evidence-purge, and evidence infra (verify against `hosted/terraform/`)
- [ ] 1.2 Write the prod-stack decision into `docs/` — pilot on the dev-prefixed stack; cut a dedicated stack only at customer #2
- [ ] 1.3 Record the deferred DynamoDB-prefix migration steps (export/import single table, re-point `Api__PublicUrl` + repo vars, soak, retire old tables)

## 2. History-cache expiry (prerequisite for any public flip)

- [ ] 2.1 **Needs the user to run this** — email GitHub Support to expire cached pre-rewrite SHAs on `ReleaseTwin` and `NAHA`
- [ ] 2.2 **Needs the user to run this** — receive written confirmation; fresh clone + `git rev-list --all | git grep -i` for prior-vendor terms returns zero
- [ ] 2.3 Confirm `company-and-domain-launch` §6.7 (licensing legal review) is complete

## 3. Repo visibility

- [ ] 3.1 **Needs the user to run this** — polish `ReleaseTwin` repo description + topics; confirm `SECURITY.md` contact is on the company domain
- [ ] 3.2 **Needs the user to run this** — flip `ReleaseTwin` to public (only after 2.2 + 2.3)
- [ ] 3.3 Decide whether `NAHA` goes public or stays private as the demo target (design Open Question); flip only if decided yes
- [ ] 3.4 Post-flip: confirm the Vercel Preview demo target still builds and renders

## 4. Open self-serve sign-up

- [ ] 4.1 Verify `company-and-domain-launch` §7 (Polar in production, `POLAR_UPGRADE_ENABLED=true`) is complete
- [ ] 4.2 Walk the funnel end to end on the live site: sign-up → org provisioned → first project created → entitlements reflect Free tier
- [ ] 4.3 Wire the sign-up link/CTA into the marketing site nav and the pricing page CTA
- [ ] 4.4 `next build` + `npx eslint` green
- [ ] 4.5 **Needs the user to run this** — deploy; do one real external sign-up + upgrade-to-Team round trip; confirm the webhook and entitlement change land
- [ ] 4.6 Confirm `ADMIN_OPERATOR_USER_IDS` repo var is set (Enterprise tier endpoint + admin surface depend on it)

## 5. Close-out

- [ ] 5.1 `docs/` go-public runbook records what was flipped, when, and the prod-stack decision
- [ ] 5.2 `openspec validate go-public-sequence --strict` passes
- [ ] 5.3 Confirm with the user before archiving
