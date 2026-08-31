## Why

The flag-proof verdict — "this case was known-bad on the old code and known-good on the
new code" — is exactly the thing a reviewer wants to see on a pull request. Today it only
exists as text in a CI log that nobody reads unless the build is red. Surfacing it as a PR
comment + status check is the cheapest way to make ReleaseTwin part of the review loop
instead of a job that runs off to the side.

Per the decision on this: **Phase 1 is a free, open-source GitHub Action** that consumes
the CLI's own output on the runner and posts to the PR. Execution stays entirely in the
customer's CI; nothing hosted is involved, so there is nothing to gate. The hosted,
Team-gated piece (per-PR history, dashboard deep-links) is Phase 2 and explicitly
deferred.

## What Changes

- **CLI: an optional machine-readable run summary.** A `--summary-json <path>` flag (and
  `RELEASETWIN_SUMMARY_JSON` env equivalent) makes the CLI write a JSON summary of the run
  after it finishes — regardless of pass/fail — alongside its normal human output. Shape:

  ```jsonc
  {
    "schemaVersion": 1,
    "overall": "passed",              // passed | failed
    "totals": { "passed": 12, "failed": 1, "cases": 13 },
    "flagProof": { "proven": 3, "ineligible": 1, "regressed": 0 },
    "cases": [
      { "id": "HTTP-DEMO-1", "outcome": "passed", "classification": null, "flagProof": null, "release": "4.2" },
      { "id": "CLM-042", "outcome": "failed", "classification": "infrastructure", "flagProof": null, "release": "4.2" }
    ]
  }
  ```

  No flag means no file written — current behavior is untouched. The summary carries only
  the metadata the CLI already prints (ids, outcomes, classifications, flag-proof results,
  the `release` label from `release-readiness-rollup`) — no bodies, no secrets.

- **`integrations/github-action/`** — a new top-level directory, **Apache-2.0 licensed**
  (so anyone can fork and adapt a PR-commenter freely; separate `LICENSE` in that
  directory, noted in the repo's `REUSE.toml` / licensing change):
  - a composite GitHub Action (`action.yml`) that runs the CLI container with
    `--summary-json`, then renders the summary into:
    - a **PR comment** (created once, updated in place on re-runs — keyed by a marker
      comment), showing the totals, the flag-proof verdict, and a table of failing /
      proven cases
    - a **check run** (`ReleaseTwin` / neutral-pass-fail) with the same summary
  - inputs: `cases-path`, `image` (pinned CLI image tag), `env-file` / passthrough vars,
    `comment` (on/off), `check` (on/off)
  - it uses only the workflow's `GITHUB_TOKEN` and the Checks/PR APIs — no ReleaseTwin
    account, no hosted call
  - a README with a copy-paste workflow snippet

- **Docs**: `docs/ci.md` (and the `/docs/ci` marketing page) gain a "PR annotations"
  section pointing at the Action.

## Capabilities

### Added Capabilities

- `ci-pr-integration`: a machine-readable CLI run summary, plus an open-source GitHub
  Action that renders it as a pull-request comment and a check run using only the
  workflow's own token — no hosted service involved.

### Modified Capabilities

- `cli-runner`: an optional `--summary-json` / `RELEASETWIN_SUMMARY_JSON` output writes a
  versioned JSON run summary; absence preserves current behavior exactly.

## Impact

- `src/ReleaseTwin.Cli/` — summary DTO + writer, wired into both the cases-directory and
  hosted-journey run paths; flag parsing in `CliEntrypoint`.
- `tests/ReleaseTwin.Cli.Tests/` — summary written on pass, on fail, shape/schemaVersion,
  flag absent → no file, flag-proof + release fields populated.
- `integrations/github-action/` — `action.yml`, the render script (Node or a small shell +
  `jq`; decide in implementation), `LICENSE` (Apache-2.0), `README.md`.
- `docs/ci.md`, `web/src/app/(marketing)/docs/ci/page.tsx` — PR-annotations section.
- The licensing change (`open-source-licensing`) / `REUSE.toml` — register
  `integrations/` as Apache-2.0.
- **No hosted API change.** **No entitlement gate** (Phase 1 is free). `ciIntegration`
  stays in the catalog as `true` for all tiers, reserved for the Phase 2 hosted piece.
- **Depends on** nothing hard; the `release` field in the summary is nicer with
  `release-readiness-rollup` but degrades to `null` without it.

## Open Questions

- Render script language: bundled Node script vs `bash` + `jq` in the composite action.
  Proposed: **small Node script** — the PR-comment upsert and check-run API calls are
  fiddly in bash; Node is already how most Actions do this and needs no extra runtime on
  `ubuntu-latest`.
- Marketplace publish: out of scope for this change (it's a manual, account-bound step).
  The Action is usable by `uses: ernestoalejowitt22/ReleaseTwin/integrations/github-action@<ref>`
  without publishing.
- Should `--summary-json` also emit on the plain (non-flag-proof) path? Proposed: **yes,
  always** — the Action wants the totals regardless.
- SARIF instead of / in addition to a check run? Proposed: **not now** — SARIF is for code
  scanning; a check run + comment is the right surface for a release gate.
