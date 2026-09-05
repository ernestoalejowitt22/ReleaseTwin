## Why

GitHub and GitLab each get a packaged, declarative CI integration — the Action and
the CI/CD Component — but Bitbucket Pipelines users are still told to hand-copy a raw
`image:` + `script:` block from `docs/ci.md`. Bitbucket has its own equivalent
mechanism for this, a "Pipe" (a versioned, parameterized Docker step referenced as
`pipe: docker://<image>:<tag>` with a `variables:` block), and unlike the other two
follow-up items tracked alongside this one, it needs no third-party account or
external approval to ship and use — Atlassian's `official-pipes` catalog listing is
a separate, optional, externally-reviewed step with no guaranteed timeline, not a
precondition for the pipe working. This is the cheapest of the three remaining
"packaged integration" gaps and the only one buildable right now.

## What Changes

- New `integrations/bitbucket-pipe/` directory: a thin wrapper image, built FROM the
  existing published CLI image, whose entrypoint reads Bitbucket pipe `variables:`
  (as environment variables) and invokes the CLI with the equivalent CLI arguments —
  the same `--junit-xml` output Bitbucket Pipelines already ingests with zero
  configuration (per `docs/ci.md`'s existing "no configuration key needed" note).
- `pipe.yml` metadata (name, image reference, declared variables and defaults) per
  Bitbucket's pipe spec.
- A README for the pipe, mirroring the shape of the existing
  `integrations/github-action/README.md` / `integrations/gitlab-component/README.md`
  (usage snippet, licensing note, no-account-required statement).
- `docs/ci.md`'s Bitbucket Pipelines section gains the pipe form as the documented
  snippet, with the existing raw `image:`/`script:` form kept as a fallback for
  callers who don't want to pull an extra image.
- The release process gains a step that builds and pushes the pipe's wrapper image
  to GHCR on each tagged release and advances `pipe.yml`'s pinned image reference to
  the new digest — the same pattern `.github/workflows/release.yml` already uses to
  pin the GitHub Action's default image (`sed`-replace a pinned `ghcr.io/.../cli@sha256:...`
  reference after build+test+push succeed).
- Deferred, explicitly out of scope: submitting the pipe to Atlassian's
  `official-pipes` catalog for marketplace listing — external review, no guaranteed
  timeline, and not required for the pipe to work via a direct `pipe: docker://...`
  reference (tracked separately if/when pursued).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `ci-pr-integration`: gains a third packaged platform integration (the Bitbucket
  Pipe), alongside the existing GitHub Action and GitLab CI/CD Component
  requirements already owned by this capability.

## Impact

- New: `integrations/bitbucket-pipe/` (wrapper image source, entrypoint script,
  `pipe.yml`, `README.md`, `LICENSE` — Apache-2.0, consistent with the other two
  integrations, independent of the AGPL engine license).
- Modified: `docs/ci.md` (Bitbucket section), `.github/workflows/release.yml` (new
  build/push/pin step for the pipe image, alongside the existing Action-pinning
  step).
- No core/adapter model change — this composes the already-published CLI container,
  the same way the GitHub Action does.
- No manual/infra steps to ship the self-serve path. The optional
  `official-pipes` catalog submission (if pursued later) would need a PR to
  Atlassian's repository and is not scoped here.
