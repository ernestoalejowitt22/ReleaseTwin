## Context

See `proposal.md` - Why for motivation. Constraints that shape this design:

- Neither a Bitbucket nor an Azure DevOps account currently exists anywhere in
  this project (checked 2026-09-03: no relevant secrets/vars in either
  `ReleaseTwin` or `releasetwin-platform`). Account creation is a manual,
  user-only step this design cannot route around.
- Bitbucket Pipelines only runs `bitbucket-pipelines.yml` for a repo Bitbucket
  Cloud actually hosts — unlike Azure Pipelines, which can build directly from
  an external GitHub repo without hosting a copy.
- The three archived/planned demo apps (`express-demo`, `react-demo`,
  `angular-demo`) each need their own toolchain (Node + npm) and a boot step
  before the CLI can run a case against `localhost` — none of this belongs in
  the .NET-only, AGPL-licensed engine repo.

## Goals / Non-Goals

**Goals:**
- One new repo, `releasetwin-ci-examples`, holding all three demo apps and all
  three non-GitHub-Actions-already-proven CI configs.
- GitHub Actions verified green in this change, without new infra.
- Bitbucket Pipelines and Azure Pipelines configs written, valid YAML, and
  ready to go live the moment the corresponding account exists — not blocked
  on code, only on the account.

**Non-Goals:**
- Getting the account signups done — that's the user's step, tracked as a task
  here but not performable by an agent (account creation is off-limits
  regardless of permission).
- CircleCI or Jenkins live proof — `docs/ci.md` already documents those
  snippets and neither was named as a funnel gap; out of scope unless asked
  for later.
- Changing anything in the engine repo's `examples/cases-express/` or
  `examples/cases-spa/` case YAML — those already ship; this change only adds
  the apps they run against and the pipelines that run them.

## Decisions

**New repo, not a folder in `ReleaseTwin`.** The engine repo is intentionally
.NET-only (per `repo-split`'s trim) and AGPL-3.0 + adapter-exception licensed.
Node-toolchain demo apps meant for permissive reuse belong in their own
Apache-2.0 repo, which is also what both deferred-scope notes explicitly asked
for. Alternative considered: a `demo-apps/` folder in `ReleaseTwin` guarded by
`.github/workflows` path filters — rejected, since it reintroduces exactly the
"Node toolchain in the engine repo" problem `repo-split` deliberately avoided
for `hosted/`/`web/`, and mixes two licenses in one repo.

**GitHub primary, mirrored to Bitbucket for Bitbucket Pipelines** (user
decision, 2026-09-03). `releasetwin-ci-examples` lives on GitHub — the funnel
surface developers already clone from — with a scheduled or push-triggered
mirror job that push-mirrors `main` to a Bitbucket Cloud repo under the same
name. `bitbucket-pipelines.yml` lives in the repo (so it travels with the
mirror) and runs natively there. Azure Pipelines needs no mirror: an Azure
DevOps pipeline can point directly at the GitHub repo as an external Git
source. Alternatives considered: Bitbucket-only scope (rejected by the user —
works against the funnel) and Bitbucket-primary-mirrored-to-GitHub (rejected —
same reason).

**One case shape, three pipeline wrappers.** Each of the three CI configs does
the same three things — checkout, boot the relevant demo app in the
background, run `ghcr.io/ernestoalejowitt22/releasetwin/cli:<pinned-version>`
against the matching `examples/cases-*` case (checked out from `ReleaseTwin` as
a shallow clone or fetched case file, TBD in tasks) — so there is exactly one
behavioral story to keep in sync, not three divergent ones. Pin the CLI image
tag to the current release (`0.2.0` today) and bump it as part of this repo's
own release checklist, matching the convention `docs/ci.md`'s snippets already
use.

**Boot-and-run, no live-account dependency for the demo apps themselves.** All
three demo apps are self-contained (no database, no external API) exactly as
scoped in the two deferred proposals, so the pipelines' only blocker is the CI
platform account, never a third-party credential.

## Risks / Trade-offs

- **Mirror job is a new maintenance surface** (auth token to push to Bitbucket,
  a job that can silently stop mirroring) → mitigate by running the mirror as
  a required step before the Bitbucket Pipelines task, not a best-effort
  background job — if the mirror push fails, that failure is visible in
  GitHub Actions, not silently stale on Bitbucket.
- **Three CI platforms to keep green** → mitigate by keeping the case + demo
  app identical across all three; a platform-specific pipeline file is only
  ever "boot + run the same CLI image," minimizing platform-specific drift.
- **Azure DevOps org name/project structure is unknown until the user creates
  one** → the `azure-pipelines.yml` in this repo is platform-agnostic (no
  hardcoded org/project); the org-specific wiring (connecting the pipeline to
  the repo) is a manual step, same treatment as Bitbucket's workspace setup.
- **CLI image version drift** → `docs/ci.md`'s existing snippets already pin a
  version and already need manual bumps per release; this repo inherits the
  same known trade-off rather than introducing a new one.

## Migration Plan

Two-phase, sequenced by what's actually unblocked:

1. **Now (no new accounts needed):** create the repo, both SPA + Express demo
   apps, the shared case story, and `.github/workflows/*.yml`. Verify green on
   GitHub Actions. This alone proves the demo apps and the CLI wiring work.
2. **Once the user has Bitbucket + Azure DevOps accounts:** wire the mirror job,
   connect the Bitbucket Pipeline and Azure Pipeline to their repos, get one
   real green run on each, add the `docs/ci.md` pointer back in the engine
   repo.

No rollback concerns — this is a new, additive repo with no engine-repo
coupling until the final `docs/ci.md` pointer task, which is a one-line link
add/revert if needed.

## Open Questions

- Exact demo-app-to-case wiring mechanism (does the CI job fetch
  `examples/cases-express/`/`examples/cases-spa/` from `ReleaseTwin` via a
  shallow clone, a pinned-ref checkout action, or a vendored copy in
  `releasetwin-ci-examples`?) — doesn't change the repo split or the pipeline
  approach either way; left for `tasks.md` to pick the simplest option during
  implementation.
- Bitbucket workspace name and Azure DevOps org/project name — the user's to
  pick when creating those accounts; doesn't affect this design.
