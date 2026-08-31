## 1. CLI run summary

- [ ] 1.1 `src/ReleaseTwin.Cli/RunSummary.cs` — DTO (`schemaVersion`, `overall`, `totals`,
      `flagProof`, `cases[]`) + a writer.
- [ ] 1.2 `CliEntrypoint` — parse `--summary-json <path>`; `RELEASETWIN_SUMMARY_JSON` env
      fallback; thread the resolved path into both run paths.
- [ ] 1.3 `CliRunner` — build the summary from the same results it prints; write after the
      run regardless of exit code.
- [ ] 1.4 Populate `release` from the case model (`release-readiness-rollup`); `null` when absent.
- [ ] 1.5 Tests: written on pass, written on fail, `schemaVersion` present, flag absent →
      no file, flag-proof + release fields, path in a non-existent dir → clear error.

## 2. GitHub Action

- [ ] 2.1 `integrations/github-action/action.yml` — composite action: run the pinned CLI
      image with `--summary-json`, then invoke the render script.
- [ ] 2.2 Render script (Node) — parse the summary, upsert a marker-keyed PR comment
      (totals, flag-proof verdict, failing/proven case table), create/update a check run.
- [ ] 2.3 Inputs: `cases-path`, `image`, `env-file`, passthrough vars, `comment`, `check`.
      Permissions documented (`pull-requests: write`, `checks: write`).
- [ ] 2.4 `integrations/github-action/LICENSE` — Apache-2.0. `integrations/github-action/README.md`
      — copy-paste workflow snippet.
- [ ] 2.5 Register `integrations/` as Apache-2.0 in the repo licensing config
      (`REUSE.toml` — coordinate with `open-source-licensing`).

## 3. Dogfood + docs

- [ ] 3.1 Add a workflow to this repo that runs the Action on PRs against the HTTP example
      (proves the comment + check path end to end).
- [ ] 3.2 `docs/ci.md` + `web/src/app/(marketing)/docs/ci/page.tsx` — "PR annotations" section.

## 4. Validation

- [ ] 4.1 `openspec validate ci-pr-integration --strict` passes.
- [ ] 4.2 `dotnet build` + `dotnet test` green; report counts.
- [ ] 4.3 The dogfood workflow posts a comment + check on a test PR (evidence in the PR).

## Decisions to lock (from proposal Open Questions)

- [ ] D1 Render script is a bundled Node script. (proposed)
- [ ] D2 Marketplace publish is out of scope; `uses:` path reference works without it. (proposed)
- [ ] D3 `--summary-json` always emits, flag-proof path or not. (proposed)
- [ ] D4 No SARIF in this change. (proposed)
