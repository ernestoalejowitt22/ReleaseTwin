## 1. CLI run summary

- [x] 1.1 `src/ReleaseTwin.Cli/RunSummary.cs` — DTO (`schemaVersion`, `overall`, `totals`,
      `flagProof`, `cases[]`) + a writer.
- [x] 1.2 `CliEntrypoint` — parse `--summary-json <path>`; `RELEASETWIN_SUMMARY_JSON` env
      fallback; thread the resolved path into both run paths.
- [x] 1.3 `CliRunner` — build the summary from the same results it prints; write after the
      run regardless of exit code.
- [x] 1.4 Populate `release` from the case model (`release-readiness-rollup`); `null` when absent.
- [x] 1.5 Tests: written on pass, written on fail, `schemaVersion` present, flag absent →
      no file, flag-proof + release fields, path in a non-existent dir → clear error.

## 2. GitHub Action

- [x] 2.1 `integrations/github-action/action.yml` — composite action: run the pinned CLI
      image with `--summary-json`, then invoke the render script.
- [x] 2.2 Render script (Node) — parse the summary, upsert a marker-keyed PR comment
      (totals, flag-proof verdict, failing/proven case table), create/update a check run.
- [x] 2.3 Inputs: `cases-path`, `image`, `env-file`, passthrough vars, `comment`, `check`.
      Permissions documented (`pull-requests: write`, `checks: write`).
- [x] 2.4 `integrations/github-action/LICENSE` — Apache-2.0. `integrations/github-action/README.md`
      — copy-paste workflow snippet.
- [x] 2.5 Register `integrations/` as Apache-2.0 in the repo licensing config
      (`REUSE.toml` — coordinate with `open-source-licensing`).

## 3. Dogfood + docs

- [x] 3.1 Add a workflow to this repo that runs the Action on PRs against the HTTP example
      (proves the comment + check path end to end).
- [x] 3.2 `docs/ci.md` + `web/src/app/(marketing)/docs/ci/page.tsx` — "PR annotations" section.

## 4. Validation

- [x] 4.1 `openspec validate ci-pr-integration --strict` passes.
- [x] 4.2 `dotnet build` + `dotnet test` green; report counts.
- [ ] 4.3 The dogfood workflow posts a comment + check on a test PR (evidence in the PR).
      **Needs the user to run this** — requires opening a real PR so
      `.github/workflows/pr-annotations.yml` fires against GitHub's live PR/Checks APIs.
      The workflow, Action, and render script are complete; this is the live-fire proof.

## Decisions to lock (from proposal Open Questions)

- [x] D1 Render script is a bundled Node script. (proposed)
- [x] D2 Marketplace publish is out of scope; `uses:` path reference works without it. (proposed)
- [x] D3 `--summary-json` always emits, flag-proof path or not. (proposed)
- [x] D4 No SARIF in this change. (proposed)
