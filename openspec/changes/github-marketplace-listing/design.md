## Context

See proposal.md - Why for the license-display constraint that rules out promoting
`action.yml` in place. `.github/workflows/release.yml` already ends its single
release job with several side-effecting steps against already-published artifacts
(pin the Action's default image, build/pin the Bitbucket Pipe, move floating
`v<major>`/`v<major>.<minor>` tags on this repo) — all gated on the same
build-and-test success. Mirroring `integrations/github-action/` to
`releasetwin-action` is one more such step, not a new release surface.

## Goals / Non-Goals

**Goals:**
- Every tagged release of `ReleaseTwin` deterministically reproduces
  `integrations/github-action/`'s exact content on `releasetwin-action`'s default
  branch and version tag, with no manual copy step.
- `releasetwin-action` never diverges from being a pure function of
  `integrations/github-action/` at release time — nothing is ever hand-edited there.

**Non-Goals:**
- No change to `integrations/github-action/`'s own content, tests, or the
  subdirectory `uses:` path's behavior.
- No general-purpose multi-repo mirroring tool — this is one narrow, one-directory
  mirror for one purpose.
- No attempt to give `releasetwin-action` its own independent commit history
  shaped differently from what `git subtree split` produces (see Decisions) —
  it's a publish target, not a project with its own development history.

## Decisions

**`git subtree split` + force-push, not a hand-rolled rsync-and-commit script.**
`git subtree split --prefix=integrations/github-action` produces a branch whose
tree is exactly that directory's contents at repo root, with the subset of commit
history that touched it — free history/attribution, and no custom diffing logic to
write or maintain. That branch is force-pushed to `releasetwin-action`'s `main`
(and tagged with the release version). Alternative considered: compute a diff and
append a single commit to `releasetwin-action`'s own independent history,
preserving a conventional non-force-pushed history there. Rejected — this
requires custom logic (checkout the mirror, diff against the source tree, commit,
handle deletions) for a repo the design's own Goal states will never carry
independent commits; `subtree split` gives a correct result for free, and
force-pushing a pure-mirror branch loses nothing since nothing is ever committed
there directly.

**One prerequisite this creates**: `releasetwin-action`'s `main` branch must NOT
have branch protection requiring PRs or blocking force-pushes, or the mirroring
step will fail. This is a one-time repo-settings item alongside creating the repo
(see proposal.md - Impact) — call it out in tasks.md so it isn't discovered only
when the first release fails.

**Floating `v<major>`/`v<major>.<minor>` tags are mirrored too.** `release.yml`
already moves these on `ReleaseTwin` itself (`cli-distribution`) so
`uses: .../integrations/github-action@v0` tracks patches. The same tags are moved
on `releasetwin-action` in the same step, for the same reason and the same
audience — anyone pinning `uses: ernestoalejowitt22/releasetwin-action@v0`.

**The mirroring step lives in the existing release job, not a separate workflow.**
Every other release side-effect (image pin, Bitbucket Pipe pin, floating tags)
already lives in `release.yml`'s one job, gated on the same build-and-test
success. Alternative considered: a separate `workflow_run`-triggered workflow.
Rejected for consistency with the established pattern and to keep one release
run as the single source of "did this succeed," rather than splitting the signal
across two workflow runs.

**Authentication: a fine-grained PAT stored as a repo secret, not a GitHub App or
deploy key.** A deploy key is repo-specific and read-only-by-default, awkward for
a bot needing write access to a *different* repo from the one the workflow runs
in; a GitHub App is the more "correct" long-term answer but is real additional
setup (App registration, installation, private key management) disproportionate
to one narrow mirroring step. A fine-grained PAT scoped to only
`releasetwin-action` with Contents: Read and write is the smallest-blast-radius
credential that does the job, consistent with this project's existing use of
narrowly-scoped tokens elsewhere (e.g. the OIDC-exchanged NuGet key, the
`releasetwin/e2e/*`-scoped AWS role).

## Risks / Trade-offs

- [The mirroring step fails silently and `releasetwin-action` goes stale without
  anyone noticing] → the step's own failure fails the release job (same gate
  pattern as every other release step here); a stale mirror is loud (CI red), not
  quiet.
- [Force-pushing to `releasetwin-action` is unusual and could look alarming in a
  diff/log] → documented explicitly in this file and in the mirroring step's own
  comment (mirroring the existing convention of inline comments explaining
  unusual release-workflow steps, e.g. the Action's digest-pin step) as
  intentional for a repo that carries no independent history.
- [The fine-grained PAT expires (fine-grained PATs support expiry) and releases
  silently stop mirroring] → tasks.md calls out setting it with no expiry or a
  long expiry, and the step's own failure (auth error) still fails the release
  job loudly rather than degrading silently.

## Migration Plan

No migration for existing users — this is purely additive (see proposal.md - What
Changes). One-time setup before this ships: the user creates
`ernestoalejowitt22/releasetwin-action` (empty, Apache-2.0, no branch protection
on `main`) and the fine-grained PAT secret. The first tagged release after that
populates the repo for the first time; the user then does the one-time "Publish
to Marketplace" step from GitHub's release UI (see proposal.md - Impact). Every
release after that is fully automatic.
