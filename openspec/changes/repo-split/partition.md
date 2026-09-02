# Path partition (task 1.2 — reviewed)

`PUBLIC` = trimmed `ReleaseTwin`. `PRIVATE` = new `releasetwin-platform`.
`filter-repo --path <PRIVATE set>` builds PRIVATE; `filter-repo --invert-paths
--path <PRIVATE set>` builds PUBLIC. Root files are reconciled by hand after.

## Open questions — resolved

1. `.claude/` `.cursor/` `.agents/` → **PUBLIC keeps them** (generic OpenSpec
   skills, useful to contributors). `CLAUDE.md` company/hosted references
   stripped in the PUBLIC copy; full version in PRIVATE.
2. `data-export` → **doc PUBLIC** (`docs/data-export.md` — the documented
   anti-lock-in guarantee), **spec PRIVATE** (`openspec/specs/data-export/` — a
   hosted-service contract).
3. `demo/record.sh` → **PUBLIC**, with the AWS Secrets Manager path for the
   LaunchDarkly credential genericised to a placeholder.

## Directories

| Path | Repo |
|---|---|
| `src/**` | PUBLIC |
| `tests/**` (all 7 are engine/adapter/CLI) | PUBLIC |
| `examples/**` | PUBLIC |
| `demo/**` | PUBLIC (genericise `record.sh`) |
| `integrations/**` | PUBLIC |
| `.claude/** .cursor/** .agents/**` | PUBLIC |
| `hosted/**` | PRIVATE |
| `web/**` | PRIVATE |

## `openspec/specs/` (34 → 15 public / 19 private)

**PUBLIC (engine):** adapter-sdk, case-loading, case-scaffolding, cli-packaging,
cli-runner, core-execution, evidence-capture, feature-flags, flag-proof,
http-adapter, http-flag-control, launchdarkly-adapter, ui-adapter,
ci-pr-integration, enterprise-access

**PRIVATE (hosted):** abuse-rate-limiting, account-provisioning,
adapter-credentials, billing, billing-metrics-digest, dashboard, data-export,
evidence-sharing, evidence-store, hosted-journeys, ingest-api, landing-demo,
marketing-site, onboarding-activation, org-membership, plan-catalog,
plan-tier-gating, project-connections, project-secrets, release-rollup,
run-notifications, supply-chain-assurance, trend-analytics, upload-staleness,
usage-metering, value-capture

_(supply-chain-assurance covers the hosted deploy chain + fork-secret boundary;
a public-relevant slice could be re-extracted later if the Action needs it.)_

## `openspec/changes/`

| Path | Repo |
|---|---|
| `openspec/changes/archive/**` (all 52) | PRIVATE |
| `openspec/changes/go-public-sequence/**` | PRIVATE (rewritten there) |
| `openspec/changes/repo-split/**` | PRIVATE (this change; archived there) |

## `docs/` (17 files + ideas/)

**PUBLIC:** quickstart, install, installation-model, flag-proof, feature-flags,
ci, enterprise-access, data-export, continuity, demo-videos, support

**PRIVATE:** company-setup, go-public-runbook, customer-pilot-guide, billing,
billing-sandbox-runbook, operator-alerting, landing-demo, `legal/**`,
`ideas/**` (deferred-backlog is the roadmap; visual-flake-classification rides
along)

## `.github/`

**PUBLIC workflows:** ci.yml (retrim to `ReleaseTwin.sln` only), codeql.yml
(retrim), dependency-scan.yml (retrim), pr-annotations.yml, release.yml,
secret-scan.yml
**PRIVATE workflows:** bootstrap.yml, deploy-hosted.yml, hosted-ci.yml,
web-ci.yml, ld-http-flag-control-e2e.yml, releasetwin-demo.yml

`.github/ISSUE_TEMPLATE/**`, `PULL_REQUEST_TEMPLATE.md`, `dependabot.yml` →
PUBLIC (support-intake). PRIVATE gets its own minimal set.

## Root files

**PUBLIC (some rewritten):** `LICENSE`, `LICENSE.EXCEPTIONS`, `LICENSES/**`,
`LICENSING.md` (drop the BSL section + the "one repo" rationale),
`README.md` (rewrite: engine-first, Adapter Linking Exception up front,
hosted/pricing → link to releasetwin.com), `SECURITY.md`, `SUPPORT.md`,
`CONTRIBUTING.md`, `REUSE.toml` (drop `hosted/**`,`web/**` BUSL + hosted-path
rules), `ReleaseTwin.sln` (engine projects only), `Dockerfile`, `.dockerignore`,
`nuget.config`, `Directory.Build.props`, `flags.json`, `.gitignore`,
`.gitleaks.toml`, `CLAUDE.md` (stripped)

**PRIVATE (new):** `README.md`, `CLAUDE.md` (full), `REUSE.toml` (BUSL + hosted
paths), plus `hosted/ReleaseTwin.Hosted.slnx` + `hosted/docker-compose.yml`
already under `hosted/`.

## filter-repo PRIVATE path set (the `--path` list)

```
hosted/
web/
docs/company-setup.md
docs/go-public-runbook.md
docs/customer-pilot-guide.md
docs/billing.md
docs/billing-sandbox-runbook.md
docs/operator-alerting.md
docs/landing-demo.md
docs/legal/
docs/ideas/
openspec/changes/
openspec/specs/abuse-rate-limiting/
openspec/specs/account-provisioning/
openspec/specs/adapter-credentials/
openspec/specs/billing/
openspec/specs/billing-metrics-digest/
openspec/specs/dashboard/
openspec/specs/data-export/
openspec/specs/evidence-sharing/
openspec/specs/evidence-store/
openspec/specs/hosted-journeys/
openspec/specs/ingest-api/
openspec/specs/landing-demo/
openspec/specs/marketing-site/
openspec/specs/onboarding-activation/
openspec/specs/org-membership/
openspec/specs/plan-catalog/
openspec/specs/plan-tier-gating/
openspec/specs/project-connections/
openspec/specs/project-secrets/
openspec/specs/release-rollup/
openspec/specs/run-notifications/
openspec/specs/supply-chain-assurance/
openspec/specs/trend-analytics/
openspec/specs/upload-staleness/
openspec/specs/usage-metering/
openspec/specs/value-capture/
.github/workflows/bootstrap.yml
.github/workflows/deploy-hosted.yml
.github/workflows/hosted-ci.yml
.github/workflows/web-ci.yml
.github/workflows/ld-http-flag-control-e2e.yml
.github/workflows/releasetwin-demo.yml
```

Note: `openspec/changes/` in the PRIVATE set means PUBLIC keeps **no** archived
or active changes — only `openspec/specs/` (engine). A fresh
`openspec/changes/` dir is created empty in PUBLIC.
