## 1. Project scaffolding

- [x] 1.1 Create `ReleaseTwin.Cli` console project referencing `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, and `ReleaseTwin.Adapters.AzureDevOps`
- [x] 1.2 Create `ReleaseTwin.Cli.Tests` project
- [x] 1.3 Add both to `ReleaseTwin.sln`
- [x] 1.4 Add a YAML parsing library dependency (YamlDotNet) to `ReleaseTwin.Cli`

## 2. Case loading (`case-loading`)

- [x] 2.1 Define the YAML case-file DTO shape per design.md D1
- [x] 2.2 Implement parsing a case file into `TestCase`, including `requires:` → `RequiredCapabilities`
- [x] 2.3 Implement fixture resolution: locator → file read → SHA-256 hash → `FixtureReference`, relative to a `fixtures/` root (design.md D3)
- [x] 2.4 Implement path-containment rejection for fixture locators (`..`, absolute paths)
- [x] 2.5 Implement clear error reporting for malformed/incomplete case files, surfaced before any case executes
- [x] 2.6 Unit tests for each requirement and scenario in specs/case-loading/spec.md

## 3. CLI runner (`cli-runner`)

- [x] 3.1 Implement environment-variable credential resolution for the Azure DevOps adapter (design.md D2), with a clear startup error on a missing variable
- [x] 3.2 Implement composing a `CompositionRoot` with the Azure DevOps adapter
- [x] 3.3 Implement loading every case file in a given directory and executing each via `CaseExecutor`
- [x] 3.4 Implement per-case console output (pass/fail, classification if failed) and an overall summary line
- [x] 3.5 Implement the exit-code contract: 0 if all cases pass, non-zero otherwise
- [x] 3.6 Unit tests for each requirement and scenario in specs/cli-runner/spec.md

## 4. End-to-end verification

- [x] 4.1 Author one example case file (against the Azure DevOps adapter, using the fake-handler pattern from Phase 2's tests) and confirm it loads and executes correctly through the CLI's own code path
- [x] 4.2 Confirm a directory with one passing and one failing case produces the correct mixed report and non-zero exit code
- [x] 4.3 Confirm zero changes were needed in `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, or `ReleaseTwin.Adapters.AzureDevOps`

## 5. Change closeout

- [x] 5.1 Confirm docs/installation-model.md still accurately describes the current state (update to note a local CLI now exists, still CI-runner-first, still no packaging)
- [x] 5.2 Run `openspec validate phase3-minimal-cli --strict` and resolve any findings
