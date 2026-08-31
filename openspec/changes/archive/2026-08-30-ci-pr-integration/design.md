## Context

See proposal.md — Why. The flag-proof verdict is the thing a reviewer wants on a PR, but
today it's buried in a CI log. Phase 1 is a free, Apache-2.0 GitHub Action that consumes
the CLI's own output on the runner — nothing hosted, nothing to gate.

Constraints that shape the approach:

- `CliEntrypoint` already parses a leading subcommand (`init` / `new` / `run`) and a
  legacy no-subcommand form; both the cases-directory and the pinned-journey run paths
  funnel through `CliRunner.RunCoreAsync`, which owns the per-case loop and the
  `passed` / `failed` tallies.
- The CLI ships as a container (`Dockerfile`, published by `release.yml` to
  `ghcr.io/<repo>/cli:<version>`). `ubuntu-latest` runners have Node 20 with global
  `fetch`.
- The repo is REUSE-managed: `REUSE.toml` maps path globs to SPDX identifiers; the engine
  is `AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception`, `examples/**` is
  Apache-2.0, `hosted/**` + `web/**` are BUSL-1.1.

## Goals / Non-Goals

**Goals:**

- A versioned, metadata-only JSON run summary the CLI writes on request, on pass or fail,
  without changing any existing output or exit code.
- A self-contained GitHub Action (composite + one Node script, zero npm install) that
  renders the summary as an upsert-in-place PR comment and a check run using only
  `GITHUB_TOKEN`.
- The Action's tree is independently Apache-2.0 so it can be forked freely.

**Non-Goals:**

- Any hosted API change, entitlement gate, or `ciIntegration` wiring — that's the deferred
  Phase 2. `ciIntegration` stays `true` for all tiers.
- Marketplace publication (a manual, account-bound step — the `uses:` path reference works
  without it).
- SARIF output (wrong surface for a release gate).
- A shared compiled type between the CLI and the Action — the Action parses the JSON.

## Decisions

### D-A: `--summary-json` is parsed in `CliEntrypoint`, path threaded through to `RunCoreAsync`

The flag (and `RELEASETWIN_SUMMARY_JSON` fallback) is stripped from the args in
`CliEntrypoint` for both the `run` and legacy forms, then passed as an optional
`string? summaryJsonPath` parameter down `RunAsync` / `RunJourneyAsync` →
`RunWithConfigAsync` → `RunCoreAsync`. `RunCoreAsync` accumulates a
`List<RunSummaryCase>` in the existing per-case loop (right where it already increments
`passed` / `failed` and prints `PASS` / `FAIL` / `FLAGPROOF`) and writes the file once,
just before its final `return`, so a failing run still produces a summary.

*Alternative rejected:* a wrapper around `CliRunner` that re-derives the summary from
captured stdout. Fragile — it would re-parse the human output the summary is meant to
replace.

### D-B: destination directory is validated up front

`--summary-json /no/such/dir/out.json` is a user error worth failing fast on. `CliEntrypoint`
checks that the resolved parent directory exists immediately after parsing the flag (before
any run) and returns exit code 1 with a clear one-line message. The write itself at the end
of the run then can't fail for a missing directory.

### D-C: summary shape

```jsonc
{
  "schemaVersion": 1,
  "overall": "passed",                 // "passed" | "failed"  (failed ⇔ any case failed)
  "totals": { "passed": 12, "failed": 1, "cases": 13 },
  "flagProof": { "proven": 3, "ineligible": 1, "regressed": 0 },
  "cases": [
    { "id": "HTTP-DEMO-1", "outcome": "passed", "classification": null, "flagProof": null, "release": "4.2" },
    { "id": "CLM-042", "outcome": "failed", "classification": "infrastructure", "flagProof": null, "release": null }
  ]
}
```

- `outcome` and `classification` are lowercased (`"passed"` / `"failed"`,
  `"infrastructure"`) to match the proposal's example.
- Per-case `flagProof` is the `FlagProofOutcome` name (`"Passed"`, `"WeakOracle"`,
  `"BothFailed"`, `"Inverted"`, `"Ineligible"`) for a flag-proof case, else `null`. The
  no-feature-state-controller case (currently counted as a failure) is reported as
  `outcome: "failed"`, `flagProof: "Ineligible"`.
- Top-level `flagProof` tallies: `proven` = `Passed`, `ineligible` = `Ineligible`,
  `regressed` = the remaining discriminating-failure outcomes.
- `release` is `TestCase.Release` (from `release-readiness-rollup`), `null` when the case
  declares none.
- Written with `System.Text.Json`, camelCase, indented, trailing newline.

### D-D: the Action is a composite action + one dependency-free Node script

`integrations/github-action/action.yml` is a `composite` action with two steps: (1)
`docker run` the pinned `image` with the cases path mounted read-only and
`--summary-json` pointed at a workspace file; (2) `node
${{ github.action_path }}/render.mjs`.

`render.mjs` uses only Node 20 built-ins (`fs`, global `fetch`). It reads
`GITHUB_TOKEN`, `GITHUB_REPOSITORY`, `GITHUB_EVENT_PATH`, `GITHUB_SHA`, resolves the PR
number from the event payload, then:

- **comment:** `GET /repos/{o}/{r}/issues/{pr}/comments`, find the one containing the
  marker `<!-- releasetwin-summary -->`, `PATCH` it if found else `POST` a new one.
- **check run:** `POST /repos/{o}/{r}/check-runs` with `name: "ReleaseTwin"`,
  `head_sha: GITHUB_SHA`, `status: "completed"`, `conclusion: "success" | "failure"`, and
  the same rendered summary as the check output. (If a run for the same name+sha exists it
  posts a second — acceptable; check runs aren't deduped like the comment. Documented.)

Both steps are individually gated by the `comment` / `check` inputs.

*Alternative rejected:* `bash` + `jq`. The comment-upsert and check-run calls are fiddly
enough that Node (the way most Actions do this, no extra runtime needed) is clearly
simpler. (Locks proposal open question → D1.)

### D-E: `integrations/**` is Apache-2.0, declared in `REUSE.toml` and a local `LICENSE`

A new `[[annotations]]` block maps `integrations/**` to `Apache-2.0`, placed with
`precedence = "override"` like the `examples/**` and `hosted/**` blocks. A full
`integrations/github-action/LICENSE` (Apache-2.0 text) is also added so a fork of just that
directory carries its license. The engine's `.github/**` glob is unchanged — the dogfood
workflow under `.github/workflows/` stays under the engine license, which is fine (it's not
part of the redistributable Action).

### D-F: the dogfood workflow uses the published image

`.github/workflows/pr-annotations.yml` runs on `pull_request` with `pull-requests: write`
+ `checks: write`, and invokes `./integrations/github-action` against
`examples/cases` with a pinned `image:` tag. It proves the comment + check path against a
real PR. It deliberately uses the *published* CLI image rather than building this PR's
code — the workflow exercises the Action's rendering path, not the CLI under test (that's
what `ci.yml` is for).

## Risks / Trade-offs

- **Published-image lag** — the dogfood workflow won't reflect an unreleased CLI change to
  the summary shape until a new image is pushed. → Acceptable for a rendering smoke test;
  `RunSummary` unit tests cover the shape itself.
- **Check-run duplication on re-run** — unlike the comment, a re-run posts another check
  run for the same SHA. → GitHub shows the latest; documented in the README. A dedupe
  (list + match by `name`+`head_sha`) can be added later if it's noisy.
- **`fetch` / Node 20 assumption** — fine on `ubuntu-latest`; the README notes the runner
  requirement.
- **`reuse lint` on the stray `LICENSE`** — the `integrations/**` override covers it; if
  lint still flags the bare filename, the fix is a one-line annotation, not a redesign.

## Migration Plan

Purely additive. No flag ⇒ no summary file ⇒ current behavior byte-for-byte. The Action
and workflow are new files. Rollback is deleting `integrations/`, the workflow, and the
`REUSE.toml` block, and dropping the `summaryJsonPath` parameter — the CLI reverts with no
data or contract to unwind.

## Open Questions

- Whether to later add check-run dedupe and a job-summary (`$GITHUB_STEP_SUMMARY`) render.
  Neither changes the spec or the task breakdown — deferrable.
