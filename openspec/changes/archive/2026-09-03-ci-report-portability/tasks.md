## 1. JUnit reporter in the CLI

- [x] 1.1 Add a `JUnitReport` model + `JUnitReportWriter` in `src/ReleaseTwin.Cli`, beside `RunSummary`/`RunSummaryWriter`, emitting `testsuites`/`testsuite`/`testcase`/`failure` (no `<skipped>`) via `XmlWriter`/`XDocument` (framework-handled escaping).
- [x] 1.2 Factor the summary writer's parent-directory check into a shared helper and have both `RunSummaryWriter` and `JUnitReportWriter` call it; keep the one-line error message shape.
- [x] 1.3 Implement the outcome → JUnit mapping from design.md D3 as a single pure function: plain pass / `Passed` → pass; every other outcome → `<failure>` with the classification (plain fail) or `FlagProofOutcome` name (`WeakOracle`, `BothFailed`, `Inverted`, `Ineligible`, `ControlFailed`, `ControlUnverified`) in `message`. Never emit `<skipped>`.
- [x] 1.4 Extend `CliEntrypoint` argument extraction to also pull `--junit-xml <path>` / `--junit-xml=<path>` and `RELEASETWIN_JUNIT_XML` (argument wins), validating the destination before the run starts, mirroring `ExtractSummaryJson`.
- [x] 1.5 Feed the JUnit builder from the same per-case call site that feeds `RunSummaryBuilder`; write the report after the run on pass or fail; write nothing when unset.
- [x] 1.6 Add `--junit-xml <path>` to the CLI `--help` text.

## 2. CLI tests

- [x] 2.1 Unit test: the mapping function is total over every `FlagProofOutcome` value and both plain-case outcomes (a new enum value fails this test).
- [x] 2.2 Test: `--junit-xml` and `RELEASETWIN_JUNIT_XML` each produce a file; the argument wins when both are set; neither set writes no file and leaves output unchanged.
- [x] 2.3 Test: missing parent directory produces the one-line error and no run.
- [x] 2.4 Test: the emitted document parses as XML, root is `testsuite`/`testsuites`, `tests`/`failures` counts match the cases, no `<skipped>` is ever emitted, and special characters in a case id / message are escaped.
- [x] 2.5 Test against a run with evidence capture enabled: the report contains no body text, header values, or credential values.
- [x] 2.6 Fixture-based test: a known-bad/known-good mix produces the expected pass/`failure` per case (`CliRunnerJUnitTests` covers `Passed`→pass, `BothFailed`→failure, plain pass/fail, and `Ineligible`→failure end to end; `WeakOracle`/`Inverted`/`ControlFailed`/`ControlUnverified` are covered at unit level in `JUnitReportTests`).

## 3. GitLab CI/CD Component

- [x] 3.1 Create `integrations/gitlab-component/templates/releasetwin.yml` — one job with `spec:inputs:` for `cases-path`, `image` (default = the pinned CLI image the Action defaults to), `stage`, `job-name`.
- [x] 3.2 Job runs the CLI from the image (`image:` + `entrypoint: [""]`, `dotnet /app/ReleaseTwin.Cli.dll "$[[ inputs.cases-path ]]" --junit-xml junit.xml --summary-json summary.json`); declares `artifacts: { when: always, reports: { junit: junit.xml }, paths: [junit.xml, summary.json] }`; non-zero CLI exit fails the job.
- [x] 3.3 Confirm the component references no GitLab API token anywhere (MR note dropped from Phase 1 — see design D4); the widget is populated by the JUnit artifact alone.
- [x] 3.4 Add `integrations/gitlab-component/LICENSE` (Apache-2.0) and `README.md` — inputs table, a copy-paste `include:` example pinned to a ref, the flag-source-credentials note, and the fork-pipeline secret warning (mirror the Action README's boundary note).
- [x] 3.5 Add SPDX headers (`Apache-2.0`) to the new files; update `LICENSING.md` / REUSE config so `integrations/gitlab-component/**` is covered like `integrations/github-action/**`.
- [x] 3.6 Lint the component YAML (`spec:inputs` schema) — validate with GitLab's CI lint schema or a local YAML+schema check in the test step.

## 4. Documentation

- [x] 4.1 Add a "Other CI platforms" section to `docs/ci.md`: what `--junit-xml` produces and the outcome mapping table, calling out that a flag-proof case which could not be paired (`Ineligible` / `ControlFailed` / `ControlUnverified`) shows as a failure in the widget even though the CLI exit code does not fail on `Ineligible`, and that the summary JSON / CLI output remain the source of full flag-proof nuance.
- [x] 4.2 Add copy-paste snippets consuming `junit.xml` via the native step for Bitbucket Pipelines, CircleCI (`store_test_results`), and Azure Pipelines (`PublishTestResults`), each stating no ReleaseTwin package is needed.
- [x] 4.3 Add a GitLab subsection to `docs/ci.md` showing the component `include:` and noting the MR test widget / Tests tab populate automatically.
- [x] 4.4 Add a one-line pointer in `integrations/github-action/README.md` sending GitLab users to `integrations/gitlab-component/`.

## 5. Verification

- [x] 5.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green — **287 tests pass** (was ~270; +17 new: `JUnitReportTests` 9, `CliEntrypointJUnitTests` 5, `CliRunnerJUnitTests` 4 — the arithmetic difference vs the raw +18 is one entrypoint test the filter earlier didn't match; full-project run confirms 169 CLI tests).
- [x] 5.2 `node --test integrations/github-action/render.test.mjs` green (6/6, the exact command `pr-annotations.yml` runs) + the new `node --test integrations/gitlab-component/templates/releasetwin.test.mjs` green (6/6).
- [x] 5.3 Ran the CLI over `examples/cases-http-only` and `examples/cases` (the latter includes an Ineligible flag-proof case) with `--junit-xml`: one `<testcase>` per case, `Ineligible`→`<failure>`, classification lowercased, no `<skipped>`, no bodies/secrets. Output matches the spec.
- [x] 5.4 `openspec validate ci-report-portability --strict` — valid.

## 6. Operational follow-ups (user-owned, not blocking apply)

- [ ] 6.1 After the first CLI release exists, bump the component's default `image` to that released tag.
- [ ] 6.2 Create/confirm a `gitlab.com` project for the component (mirror or dedicated), enable the CI/CD Catalog setting, and add a release-tag pipeline in that project so the component is catalog-discoverable. Record the canonical `include:` path in the README.
