## Context

See proposal.md - Why. Three surfaces need a link added: the GitHub Action's rendered PR
comment (`integrations/github-action/render.mjs`), three README files, and one new Action
input (`action.yml`). `render.mjs`'s `renderBody` already assembles the comment as a joined
array of lines (`## ReleaseTwin — ...`, totals, table); a footer line is a fourth section
appended to that array, gated on a new boolean flag read from the environment the same way
`RELEASETWIN_COMMENT` / `RELEASETWIN_CHECK` already are.

## Goals / Non-Goals

**Goals:**
- Every PR comment the Action posts links to the landing page unless a caller explicitly
  opts out.
- The three affected READMEs are discoverable entry points to the landing page on their own,
  independent of the main repo README.

**Non-Goals:**
- No change to the check run body, the GitLab Component's rendered widget output (it has no
  custom render surface — see proposal.md - What Changes), or `action.yml`'s `description`
  field (Marketplace search-result text; a separate, higher-effort copywriting decision, not
  a mechanical link).
- No embeddable "status badge" for adopters to put in their own README — a materially larger
  feature (needs a badge-serving endpoint) considered in the same conversation and explicitly
  deferred, not folded in here.
- No analytics/tracking parameter on the link (e.g. UTM tags) — keeping the link a plain,
  static URL avoids adding any tracking-consent surface to a tool whose entire pitch is "your
  data stays put."

## Decisions

**Footer line, not inline attribution.** The link is a distinct trailing line
(`---\n[ReleaseTwin](https://releasetwin.com)`-shaped) after the existing totals/verdict
content, rather than woven into the `## ReleaseTwin — <verdict>` heading. Alternative
considered: put the link in the heading itself (`## [ReleaseTwin](...) — ...`). Rejected —
it makes the heading noisier on every render and is harder to visually opt out of at a
glance; a separate trailing line reads as attribution, not as part of the verdict.

**Opt-out via a new Action input (`attribution`, default `true`), not a hosted/silent
toggle.** Alternative considered: no opt-out at all, treating the link as non-negotiable
attribution. Rejected — the Action's whole positioning is "no ReleaseTwin account, no hosted
call, you're in control" (see `ci-pr-integration`'s existing requirements); shipping an
unremovable outbound link would sit oddly against that, and a security- or
brand-conscious team should not have to fork the Action to remove it. Default stays `true`
because the growth value only exists if most adopters don't have to opt in.

**README links added, `action.yml` `description` left alone.** The `description` field is
what shows in Marketplace search results before a click; changing its wording is a
copywriting call independent of "does this surface link to the landing page," and is left
for a separate change if wanted.

## Risks / Trade-offs

- [A team objects to any outward link appearing by default in their PR comments, even one
  they can turn off] → the `attribution` input exists specifically for this; document it
  in the README next to the other inputs so it's easy to find, not just easy to set.
- [Adding a footer line changes the exact comment body for every existing adopter on their
  next run] → this is an intentional, visible content change (not a silent behavior change);
  it does not touch the "byte-for-byte with no upload" scenario already in
  `ci-pr-integration`, which is scoped to the dashboard-URL feature's own before/after
  comparison, not a promise that comment content never changes again.

## Migration Plan

No migration — this ships as a normal Action/CLI release. Existing pinned references
(`@v0.2.0` etc.) are unaffected until an adopter bumps to the release that includes this
change, at which point the footer appears by default; `attribution: false` is available
immediately in that same release for anyone who wants it off. No feature flag needed (this
is an OSS Action/README surface, not a `flags.json`-registered surface).
