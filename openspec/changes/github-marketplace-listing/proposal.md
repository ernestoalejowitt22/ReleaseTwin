## Why

GitHub only lets a repository publish **one** Marketplace listing, and only when its
`action.yml` sits at the repository's root — a documented platform constraint, not a
preference. `integrations/github-action/action.yml` lives in a subdirectory (by
design: this is one of three integrations sharing the repo alongside
`gitlab-component` and `bitbucket-pipe`), so it cannot be listed today. Researching
the fix surfaced a second, more important constraint: GitHub's Marketplace listing
displays a repo's license from its root `LICENSE` file, and this repo's root license
is confirmed AGPL-3.0 (`gh api repos/.../ReleaseTwin --jq '.license.spdx_id'` →
`AGPL-3.0`). Promoting `action.yml` to this repo's root would make the Marketplace
listing display the Action as AGPL-3.0-licensed, when its actual license is
Apache-2.0 (`integrations/github-action/LICENSE`) — a real misrepresentation, not a
cosmetic one, and one this project has otherwise been careful to avoid (per-file
SPDX headers, `LICENSE.EXCEPTIONS`, explicit "independently of the engine's
copyleft license" language on every integration's README).

## What Changes

- New repository, `ernestoalejowitt22/releasetwin-action`, containing a mirror of
  `integrations/github-action/`'s contents (`action.yml`, `render.mjs`,
  `render.test.mjs`, `README.md`, `LICENSE`) at its root, Apache-2.0 only, with no
  other content — its root license is then correct with no adjustment needed.
- `integrations/github-action/` in this repo stays exactly as-is and remains fully
  supported: the existing documented `uses:
  ernestoalejowitt22/ReleaseTwin/integrations/github-action@v0.2.0` form does not
  change or get deprecated. **BREAKING for nobody** — this is additive.
- The release process gains a mirroring step: after a successful release, push
  `integrations/github-action/`'s tree to `releasetwin-action`'s `main` and tag it
  with the same version, using the same floating-`v<major>` pattern the Action
  already uses in this repo. This is source-of-truth-preserving — `releasetwin-action`
  is a publish target, never edited directly.
- `docs/ci.md`, `docs/install.md`, and `integrations/github-action/README.md` each
  gain the new `uses: ernestoalejowitt22/releasetwin-action@v0.2.0` form as the
  recommended one (shorter, and the one eligible for the Marketplace badge), with
  the existing subdirectory form kept as a documented alternative for anyone
  already pinned to it.
- Explicitly deferred: renaming/moving to a `releasetwin` GitHub organization
  (none exists yet — checked via `gh api orgs/releasetwin` → 404); the actual
  first-time "cut a Release and tick Publish to Marketplace" action, which is a
  one-time, browser-only step in GitHub's UI that only the repository owner can
  perform (see Impact); and the Azure DevOps Marketplace listing (separate,
  already-tracked item, blocked on a different account).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `ci-pr-integration`: gains a requirement that the GitHub Action is also
  published, unmodified, to a dedicated root-level repository so it is eligible
  for GitHub Marketplace listing, alongside the existing subdirectory-hosted form.

## Impact

- New: `ernestoalejowitt22/releasetwin-action` (created and owned by the user —
  Claude cannot create a new top-level repository on the user's account without
  that being an explicit, confirmed action; scoped here, created when approved).
- New: a repo secret on `ReleaseTwin` — a fine-grained personal access token (or
  deploy key) scoped to only `releasetwin-action`, contents:write, used by the
  release-workflow mirroring step. **The user must generate this token** (GitHub
  Settings → Developer settings → Fine-grained tokens → repository access limited
  to `releasetwin-action` → Contents: Read and write) and set it as a repo secret
  (e.g. `RELEASETWIN_ACTION_MIRROR_TOKEN`) before the mirroring step can run.
- Modified: `.github/workflows/release.yml` (new mirroring step), `docs/ci.md`,
  `docs/install.md`, `integrations/github-action/README.md`.
- **Needs the user to run this, one time, after the first successful mirror**:
  on `releasetwin-action`, use GitHub's "Draft a new release" UI, check "Publish
  this Action to the GitHub Marketplace," choose a primary category (e.g. "CI"),
  confirm the icon/color already declared in `action.yml`'s `branding:` block
  renders correctly, and publish. This is a one-time listing action; every
  subsequent tagged release on `ReleaseTwin` updates `releasetwin-action`'s code
  automatically via the mirroring step, and GitHub picks up new releases into the
  existing listing without repeating the publish flow.
- No core/adapter model change.
