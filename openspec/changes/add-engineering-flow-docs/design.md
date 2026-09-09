## Context

`releasetwin-platform` (sibling repo, same owner) already shipped an identical `add-engineering-flow-docs` change: `docs/engineering/` content plus a `docs/**` → GitHub wiki sync workflow, verified working end to end (including catching and fixing two real issues: the wiki feature being disabled by default, and a missing `Home.md` when `docs/` has no top-level README). This design reuses those decisions rather than re-deriving them, and only calls out what's different for this repo.

## Goals / Non-Goals

**Goals:**
- Give this repo's own engine-flow content (CLI, kernel, flag proof, adapters) a durable, PR-reviewed home.
- Reuse the platform repo's wiki-sync mechanics exactly, pointed at this repo.

**Non-Goals:**
- Not re-deriving the wiki-sync workflow's design from scratch — see `releasetwin-platform`'s `openspec/changes/add-engineering-flow-docs/design.md` for the full rationale (trigger choice, `GITHUB_TOKEN` auth, clone-copy-commit-push mirror strategy, Home.md generation). This design only notes deltas.
- Not documenting anything about the private platform (billing, hosted infrastructure, ticket-tracker credential handling) — this repo is public, and its docs should only describe what's already public here: the CLI and engine, whose behavior contract is already public via `openspec/specs/`.

## Decisions

**1. One file, not five.** The platform repo split its documentation into a Flow Atlas plus four deep dives because it had four actively-changing, high-stakes flows (ticket write-back, flag proof/gate, evidence lifecycle, billing). This repo's engine-side content is comparatively stable and already has 16 written specs as its authoritative source — a single `docs/engineering/overview.md` narrating the CLI/kernel/adapters and pointing into `openspec/specs/` is the right level of investment; a second layer of deep dives isn't warranted unless a specific engine flow turns out to need one.

**2. Same wiki-sync workflow, copied and re-pointed, not abstracted into a shared action.** The two repos are otherwise independent (different licenses, different release cadences) — a shared reusable workflow would couple them for a ~60-line script that's cheap to duplicate and easy to keep in sync by eye when either one changes.

**3. Enable the wiki feature via `gh api ... -f has_wiki=true` as part of implementation, same as the platform repo — this part *is* scriptable and should not be left as a manual step.** Only the first-page creation is genuinely manual.

## Risks / Trade-offs

- **[Risk]** Same as the platform repo: the wiki's git repo doesn't exist until a human creates a first page. → **Mitigation**: called out explicitly in proposal.md's Impact; the workflow's clone step fails with a specific, actionable message rather than a generic git error.
- **[Trade-off]** Duplicating ~60 lines of near-identical workflow YAML across two repos instead of a shared composite action. → Accepted, per Decision 2 — the repos are independent enough that the coupling cost would outweigh the duplication cost for a script this size.
