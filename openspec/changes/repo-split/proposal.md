## Why

`go-public-sequence` currently plans to flip the whole `ReleaseTwin` monorepo
public. That repo also holds `hosted/` + `web/` (the SaaS backend and dashboard,
BSL 1.1), `hosted/terraform/**` (the exact AWS infra, incl. account
`846136340491` and the state-bucket name), and `openspec/` — 52 archived change
proposals containing the pricing rationale, the "BSL is the only real lever vs a
competitor rehosting the dashboard" reasoning, a per-item list of deferred
security hardening, the go-public runbook, and the `company-and-domain-launch`
legal/RFC/RESICO detail.

The open-core bet only needs the **engine** to be public and loud — that is the
whole self-discovery funnel. Publishing the hosted source, the infra, and the
planning history has almost no upside (nobody contributes to billing
reconciliation) and a real, permanent downside: roadmap, known weaknesses, infra
map, the "how it works" moat, and a very legible "solo operator + AI agent,
~2 weeks old" signal for enterprise procurement. BSL's protection —
"can't rehost this code competitively" — is fully achieved by simply **not
publishing** the hosted source, with none of that exposure.

Splitting now also makes the eventual public flip (`go-public-sequence` 2.4) low
stakes: we flip a repo that is *designed* to be public, not one where we hope
nobody reads `hosted/terraform/`.

## What Changes

- **New private repo `releasetwin-platform`** — receives `hosted/**`, `web/**`,
  `hosted/terraform*`, the ops/business `docs/`, the hosted `openspec/specs/`,
  `openspec/changes/archive/**`, `go-public-sequence`, and the hosted/deploy
  `.github/workflows/`. Full git history, `filter-repo --path`.
- **`ReleaseTwin` is trimmed to the engine** — `src/**` (Core, AdapterSdk, the
  5 adapters, Cli), `tests/**` (engine/adapter/CLI only), `examples/**`,
  `demo/**`, `integrations/github-action/**`, the user-facing `docs/`, the
  engine `openspec/specs/`, and the engine `.github/workflows/`. Full history,
  `filter-repo --invert-paths` of the private set. Keeps the name, the domain,
  the stars, the issue history.
- **Root files reconciled** — `ReleaseTwin.sln` becomes engine-only;
  `REUSE.toml` drops the `hosted/**`/`web/**` (BUSL) and hosted-path rules;
  `README.md` is rewritten to lead with the engine + the Adapter Linking
  Exception; `CLAUDE.md` splits into an engine-dev public version and a full
  private version.
- **`go-public-sequence` is updated in the private repo** — 2.4 becomes "flip
  the (now engine-only) public `ReleaseTwin` repo"; the history-cache /
  repo-visibility section is re-scoped to the engine repo.
- **No behavior change.** No engine, adapter, hosted API, or CLI contract moves.
  `skip_specs: true` — specs are relocated between repos, not modified.

Not in scope: renaming anything the public already sees; changing the AGPL /
Adapter Exception / BSL license terms; squashing history (keep full, scrubbed);
publishing `releasetwin-platform` (it stays private indefinitely).

## Capabilities

_None — `skip_specs: true`. This is a repository-topology and process change; no
requirement is added, modified, or removed. The `openspec/specs/` files are
partitioned between the two repos unchanged._

## Impact

- **Two git repositories** where there was one. `releasetwin-platform` private,
  `ReleaseTwin` trimmed and (via `go-public-sequence`) eventually public.
- **AWS OIDC trust** — `hosted/terraform-bootstrap` trusts
  `repo:ernestoalejowitt22/ReleaseTwin:*`; must be re-pointed to
  `releasetwin-platform` and the bootstrap re-run from the new repo. Until then
  `deploy-hosted.yml` cannot assume its role.
- **GitHub repo secrets / variables** — every `CLERK_*`, `POLAR_*`,
  `AWS_DEPLOY_ROLE_ARN`, `WEB_BASE_URL`, `DOMAIN_NAME`,
  `NOTIFICATIONS_FROM_ADDRESS`, `CLERK_DOMAIN`, and the e2e secrets move to
  `releasetwin-platform`. `NUGET_API_KEY` and GHCR publishing stay on
  `ReleaseTwin`.
- **Vercel** — the `web/` project is connected to `ReleaseTwin`; reconnect it to
  `releasetwin-platform`.
- **Cross-repo references** — `integrations/github-action` docs, `docs/ci.md`,
  the CLI image tag, any `uses: ernestoalejowitt22/ReleaseTwin/...` — audited so
  nothing points at a path that moved.
- **CI** — the public repo's `ci.yml` no longer builds `hosted/` or `web/`;
  `hosted-ci.yml` / `web-ci.yml` / `deploy-hosted.yml` run in the private repo.
- **Open PRs at split time** — must be merged or closed first; a `filter-repo`
  run invalidates every open branch.
- **`git filter-repo` force-push on `ReleaseTwin`** — third history rewrite;
  same accepted residual as `go-public-sequence` §2 (private repo, 0 forks).
  Local clones + the operator's other machines re-clone.
