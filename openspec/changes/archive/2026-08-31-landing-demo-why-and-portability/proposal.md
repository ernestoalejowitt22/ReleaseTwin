## Why

The landing page shows the mechanics of ReleaseTwin — a failing PR check, the PR comment,
the dashboard — but never states the problem those mechanics solve, so a first-time
visitor sees *how it works* before *why they would want it*. It also shows the merge gate
only on GitHub, which under-sells the actual portability story: the deliverable is a
CI-agnostic `--summary-json` contract, not a GitHub integration, and the Bitbucket
Pipelines snippet that proves this is buried in `/docs/ci` instead of on the demo.

## What Changes

- Add a **problem / value section** to the landing page, placed above the "One loop"
  demo, stating the failure mode ReleaseTwin catches (contract drift, expired sandbox
  credentials, downstream failures that only appear under real auth — found post-release,
  from a customer) and what a team gains (a required check instead of a manual checklist;
  a readable PR verdict instead of a Slack thread; linkable redacted evidence instead of
  terminal scrollback; execution and data staying in your runner; flag-proof that a real
  credentialed path ran).
- **Promote CI portability onto the landing page.** The demo's CI-loop panels gain a
  non-GitHub panel derived from the same `--summary-json` contract — the Bitbucket
  Pipelines YAML snippet and/or a generic pipeline-log render — captioned to make clear
  the verdict is identical on any CI and is not a GitHub-specific integration.
- Keep the existing GitHub PR check + comment panels as the headline (the packaged,
  one-line-install path) and keep the per-panel claim captions.
- The animated hero terminal recording stays as a supporting "under the hood" element,
  demoted below the new problem/value section if layout requires it.

### Explicitly deferred (not in this change)

- A **packaged Bitbucket integration** (a Pipelines pipe rendering a real "ReleaseTwin
  passed/failed" report on a Bitbucket PR) and any Bitbucket PR *screenshot*. This change
  communicates portability without implying a packaged integration exists.
- GitLab / other CI beyond the portability note.
- A video walkthrough or interactive demo.
- Richer GitHub PR-page context (full "merge blocked" banner, reviewer view) — the
  current check strips + comment are sufficient once a "why" sits above them.

### Open decision for review

Whether the non-GitHub panel is **the YAML snippet only** (cheapest, unambiguously
honest) or **also a captured generic pipeline-log render** (more concrete, needs a
capture path and a spec allowance that a pipeline-log render — not a PR screenshot — is
permitted). The specs below allow either; `design.md` should pick one.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `marketing-site`: the landing page must lead with a problem/value section before the
  merge-gate loop, and the CI-portability requirement moves from "documented in `/docs`"
  to "present on the landing demo", with an explicit rule that a pipeline-log render is
  allowed but a Bitbucket PR screenshot / packaged-integration claim is not.
- `landing-demo`: the CI-loop panel set includes a non-GitHub CI panel derived from the
  same run-summary contract, and the asset-provenance rules extend to cover it.

## Impact

- `web/src/app/(marketing)/page.tsx` — new section, new panel entry.
- `web/src/app/(marketing)/docs/ci/page.tsx` — Bitbucket snippet becomes the shared
  source for the landing panel (or is referenced from it) rather than living only here.
- Possibly `web/scripts/capture-landing-demo.mjs` / a new capture step if the generic
  pipeline-log render option is chosen.
- Marketing copy only — no API, engine, or CLI behavior changes.
