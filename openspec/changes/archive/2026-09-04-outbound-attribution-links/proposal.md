## Why

The highest-traffic surfaces where a stranger encounters ReleaseTwin without having chosen
to visit releasetwin.com currently link nowhere back to it. The GitHub Action's PR
comment — the thing every teammate on an adopting team sees on every pull request, whether
or not they personally installed anything — carries no attribution or link at all
(`render.mjs` renders `## ReleaseTwin — ✅ Passed` and stops). The NuGet package README, the
GitHub Action README, and the GitLab Component README each link sideways to `docs/*.md` on
GitHub but never to the landing page, so someone who discovers the CLI via `dotnet tool
search` or the GitHub Marketplace never sees the pitch, the flag-proof demo, or the
"design partner" signup CTA. This is the cheapest, widest-reach lever for the free-tier
self-discovery funnel — no new feature, just closing an attribution gap on paths that
already exist.

## What Changes

- The GitHub Action's rendered PR comment gains a single unobtrusive footer line linking to
  the landing page (e.g. `--- \n[ReleaseTwin](https://releasetwin.com)`), added once per
  comment render in `render.mjs`.
- A new Action input, `attribution` (boolean, default `true`), lets a caller opt out of the
  footer line without forking the Action — some teams may not want an outward link in every
  PR comment.
- The GitHub Action README and the GitLab Component README each add a link to
  releasetwin.com near the top, alongside (not replacing) the existing docs links.
- The CLI's NuGet package README (`src/ReleaseTwin.Cli/README.md`) adds a link to
  releasetwin.com near the top, alongside the existing docs links.
- No change to the GitLab Component's rendered output — GitLab's merge-request test widget
  is populated entirely from the JUnit artifact with no custom comment body, so there is no
  equivalent per-run surface to add a footer to there; only its README changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `ci-pr-integration`: the rendered PR comment gains an optional attribution footer link
  (new `attribution` input, default on); the GitHub Action README and GitLab Component
  README each document/link to the landing page.
- `cli-packaging`: the NuGet package README links to the landing page.

## Impact

- `integrations/github-action/render.mjs` — footer line in `renderBody`.
- `integrations/github-action/action.yml` — new `attribution` input.
- `integrations/github-action/README.md` — landing-page link.
- `integrations/gitlab-component/README.md` — landing-page link (docs only, no template
  change).
- `src/ReleaseTwin.Cli/README.md` — landing-page link.
- No core/adapter model change. The Action and GitLab Component are Apache-2.0 surfaces
  already independent of the AGPL engine license; the CLI README edit is a packaging-docs
  change, not a licensing one.
- Deferred (explicitly out of scope here): a control-block cookbook for other flag vendors
  (Unleash/GrowthBook/PostHog), any dedicated non-LaunchDarkly adapter, and an embeddable
  README badge for adopters' own repos — a separate, larger viral mechanism considered in
  the same conversation but not part of this change.
- No manual/infra steps — pure code and docs in the already-public repo, shipped through the
  existing release process (Action version bump, NuGet republish on next tagged release).
