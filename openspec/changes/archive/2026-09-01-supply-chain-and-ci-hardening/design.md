## Context

See `proposal.md` — Why. This change is almost entirely CI workflow + Action
config; no runtime or hosted-API code moves. Current state that shapes it:

- **Secret scanning** already exists: `.github/workflows/secret-scan.yml` runs
  `gitleaks` on PR + push + a weekly history sweep, but installs the binary via
  `curl -sSL … | tar -xz` with no checksum.
- **No** dependency scanning, **no** SAST, **no** Dependabot config today.
- **The Action** (`integrations/github-action/action.yml`) is a composite action,
  distributed by moving git tags (`uses: <repo>/integrations/github-action@v0`),
  and defaults `image` to `ghcr.io/<repo>/cli:latest`. `release.yml` builds and
  pushes `cli:<version>` **and** `cli:latest` (multi-arch) on a `v*.*.*` tag, then
  force-moves the floating `v<major>` / `v<minor>` git tags to the release commit.
- **Deploy** (`deploy-hosted.yml`) runs on push to `main` under paths filter,
  OIDC → AWS via `vars.AWS_DEPLOY_ROLE_ARN`, no `environment:` declared. The
  role's trust policy is managed by `hosted/terraform-bootstrap`.
- **Constraint — required status checks:** on a **private** repo, GitHub only
  enforces "required" status checks (auto-block merge) on a paid plan. This repo
  is not currently on one (same limitation already recorded for the NAHA repo).
  See Risks + Open Questions.

## Goals / Non-Goals

**Goals**

- Every PR runs secret + dependency + SAST checks that **fail visibly** on a new
  high-severity finding, so a green PR is a real signal.
- The published Action's default image is immutable and updated by the release
  process, with a runtime warning when a caller pins nothing.
- The deploy job runs under a named environment so protection rules and
  environment-scoped secrets become available.
- No new always-on infrastructure, no new stored credential.

**Non-Goals**

- SBOM generation, artifact signing (cosign/SLSA provenance) — follow-up.
- Making the checks *mechanically* required if that needs a plan upgrade — the
  design makes them fail; enforcement is a separate toggle (Open Questions).
- Touching the CLI image build itself, or the container's contents.
- Retroactively scanning historical dependency states.

## Decisions

### D1 — Dependency scanning: native tooling + a parse-and-fail wrapper, plus Dependabot

Two independent layers:

1. **PR gate** — a new `dependency-scan.yml` job:
   - .NET: `dotnet restore` then `dotnet list package --vulnerable --include-transitive`.
     The command exits `0` regardless of findings, so the job greps its output for
     `Critical`/`High` and fails with those lines echoed.
   - web: `npm ci --prefix web` then `npm audit --audit-level=high` (this one's
     exit code *is* usable).
2. **Ongoing** — `.github/dependabot.yml` with three ecosystems: `nuget` (solution
   root), `npm` (`/web`), `github-actions` (`/`). Weekly, grouped minor/patch.

**Why not a single third-party scanner** (Trivy, Snyk, `dotnet` + OSV): the
native tools are already on the runner or one install away, need no account or
token, and their advisory source (the GitHub Advisory Database / npm registry) is
the one a reviewer expects. Trivy adds value for the *container* — deferred with
SBOM to the follow-up.

**Known wrinkle:** `dotnet list package --vulnerable` has no severity filter and
no fail flag; the grep wrapper is unavoidable and slightly brittle across SDK
output format changes. Mitigation: pin the SDK version in the job and keep the
match broad (`grep -E 'Critical|High'`).

### D2 — SAST: CodeQL advanced workflow, two languages

A version-controlled `.github/workflows/codeql.yml` (advanced setup, not the
repo-settings "default setup") with `language: [csharp, javascript-typescript]`,
triggered on `pull_request`, `push` to `main`, and a weekly `schedule`. C# uses
an explicit `dotnet build` step (CodeQL needs the compile); JS/TS needs none.
Severity threshold: CodeQL's default (`security-extended` query suite is
optional — start with the default `security-and-quality` off, `security` on, to
keep PR noise low).

**Why advanced over default setup:** the workflow file is reviewable and
diffable, the schedule and query suite are explicit, and it composes with the
existing workflow conventions in this repo.

**Alternative rejected:** a non-CodeQL analyzer (Semgrep). CodeQL is free for this
repo, integrates with the Security tab, and covers both languages in one tool.

### D3 — Action default image: release-stamped digest + runtime mutable-tag warning

- `release.yml` gains a step after `docker/build-push-action` (which already
  outputs `digest`) that rewrites `integrations/github-action/action.yml`'s
  `image` default to `ghcr.io/<repo>/cli@<digest>` and commits it to `main`
  before the floating-tag move — so the release commit (and the `v0` / `v0.N`
  tags that point at it) carry an **immutable digest** default.
- The composite action gains a first shell step: if the effective `image` input
  matches a mutable tag pattern (`:latest`, `:v?<major>` only, or no digest and
  no `:` at all), emit `::warning::` naming the risk; always continue.
- `release.yml` **stops pushing `cli:latest`** (or keeps it only as a
  convenience that the Action no longer defaults to — decided in tasks; leaning
  toward keeping it for humans but never referencing it from the Action).

**Why digest, not `cli:<version>`:** a version tag on GHCR is still movable by
anyone with push. The digest is the content. The release process is the only
writer of the default, satisfying the spec's "updated deliberately by the release
process".

**Alternative rejected — derive the image from `github.action_ref` at runtime:**
`@v0` resolves to `github.action_ref = v0`, which is not a pushed image tag;
handling that cleanly reintroduces a mutable-tag fallback. Stamping the digest at
release time is unambiguous.

**Alternative rejected — no default, fail if unset:** better security, worse
first-run DX for a product trying to get pilots; the warn-and-continue default
plus an immutable value is the balance.

### D4 — gitleaks: verify the download, don't pipe it

Replace the `curl … | tar` in `secret-scan.yml` with: download the pinned
release tarball **and** its checksum file to disk, `sha256sum -c`, then extract.
Keep the explicit `VERSION` pin.

**Why not `gitleaks/gitleaks-action`:** that action requires a `GITLEAKS_LICENSE`
for use in an organization account; sidestepping the license question by
checksum-verifying the upstream binary is simpler and equally sound. If the repo
later moves under an org with a license, switching to the SHA-pinned action is a
clean follow-up.

### D5 — Deploy environment gate

`deploy-hosted.yml`'s `deploy` job gains `environment: production`. The
environment itself must be created in repo settings (one-time, manual — no code
path for it). Even with **no** protection rules configured, this immediately
gives: a deployment history/record, the ability to add required reviewers or a
wait timer later without touching the workflow, and environment-scoped secrets.

**Optional, deferred to tasks:** tighten the bootstrap role's OIDC trust
`sub` condition to `repo:<org>/<repo>:environment:production` so the deploy role
can *only* be assumed from a job running in that environment. This is a
`hosted/terraform-bootstrap` change that auto-applies via `bootstrap.yml`; do it
in the same PR or a fast follow.

## Risks / Trade-offs

- **Checks fail but do not mechanically block merge** on the current plan →
  Mitigation: solo maintainer merges on green as a matter of discipline; the
  checks are visible on every PR; flip on "required status checks" the moment the
  repo is on a plan that supports it (or goes public). Tracked in Open Questions.
- **`dotnet list package --vulnerable` output format drift** breaks the grep →
  Mitigation: SDK version pinned in the job; broad match; a unit-style assertion
  in the job that the command actually ran (non-empty output, `Project` header
  present) so a silent format change surfaces as a job failure, not a false pass.
- **CodeQL PR latency** (C# build + analysis is minutes) → Mitigation: it runs in
  parallel with the existing build/test jobs; not on the critical path for local
  work.
- **Dependabot PR noise** for a solo maintainer → Mitigation: weekly cadence,
  grouped minor/patch, `github-actions` ecosystem included so action pins stay
  current with one review.
- **Release step that commits `action.yml` back to `main`** adds a write from CI
  to the default branch → Mitigation: it is one deterministic line change,
  gated behind a fully-green release (build+test+push all succeeded), same trust
  model as the existing floating-tag force-push in `release.yml`.
- **Stopping `cli:latest`** could break an existing external consumer →
  acceptable and intended (**BREAKING**, per proposal); documented in the Action
  README and the release notes.

## Migration Plan

1. Add `dependency-scan.yml`, `codeql.yml`, `dependabot.yml`; harden
   `secret-scan.yml`. These are additive — merging them just starts producing
   checks. Fix or waive whatever the first run flags before relying on them.
2. Add `environment: production` to `deploy-hosted.yml`. **Needs the user:**
   create the `production` environment in repo settings first (the workflow run
   will otherwise wait/fail on an unknown environment).
3. `release.yml`: add the digest-stamp + mutable-tag-warning work; ship it so the
   **next** tagged release produces an Action whose default image is a digest.
   Until that release, the current `@v0` Action still defaults to `:latest` — call
   this out in the PR.
4. Optional: bootstrap trust-policy `sub` tightening (D5) — same PR or fast
   follow.
5. **Rollback:** every workflow is independently revertable; none changes
   deployable artifacts. Reverting `deploy-hosted.yml`'s `environment:` line
   restores the prior behavior immediately.

## Open Questions

- **Enable "required status checks" / branch protection?** Needs a repo plan that
  supports it on a private repo, or the repo going public. Until then the new
  checks are advisory-enforced-by-discipline. This does not change any spec —
  the checks genuinely fail on a finding; "blocks merge" becomes mechanical when
  the toggle is available. Decide alongside the go-public sequence.
- **Keep `cli:latest` as a human convenience** (just never referenced by the
  Action), or drop it entirely? Resolve in tasks; does not affect the spec.
- **Move `POLAR_*` / `GH_OAUTH_CLIENT_SECRET` to environment-scoped secrets**
  once the `production` environment exists — worth doing but out of this change's
  declared scope; note for a follow-up.
