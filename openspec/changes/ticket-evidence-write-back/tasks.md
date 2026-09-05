## 1. GitHub Issues write-back

- [x] 1.0 Discovered mid-implementation: the CLI's run summary never carried
      a case's `oracle.locator`, so there was nothing for a CI integration to
      resolve a ticket from. Added `oracleLocator` to `RunSummaryCase`
      (`src/ReleaseTwin.Cli/RunSummary.cs`), threaded `testCase.Oracle.Locator`
      through all three `AddCase` call sites in `CliRunner.cs`, and bumped
      `RunSummary.CurrentSchemaVersion` 2 → 3 per `ci-pr-integration`'s
      existing "adding a field increments schemaVersion" rule. Captured as a
      MODIFIED delta on `ci-pr-integration` in this change's specs (the
      original proposal's "no core/CLI changes" impact note was wrong — this
      is the one CLI-side change the rest of the capability depends on).
- [x] 1.1 Add locator parsing (`^#\d+$`) to `integrations/github-action/render.mjs`,
      resolved against `github.repository` (the run's own repo).
- [x] 1.2 Add an issue-comment poster reusing the existing `fetch`-based
      `api.github.com` helper (`POST /repos/{owner}/{repo}/issues/{number}/comments`)
      with the run's verdict, case ID, oracle locator, outcome, and evidence link.
- [x] 1.3 Add an opt-in `ticket-write-back` input to `action.yml` (default
      `"false"`), wired through to the render step's env, matching the existing
      `comment`/`check`/`attribution` input pattern.
- [x] 1.4 Skip (not fail) cases whose `oracle.locator` doesn't match the
      convention; log a one-line notice per skipped/attempted write-back.
- [x] 1.5 On a tracker API error (404/403/network), log it via
      `::warning::` and continue — never change the step's exit code or the
      case's own pass/fail outcome.
- [x] 1.6 Add/extend `render.test.mjs` cases: recognized locator posts a
      comment; unrecognized locator is skipped; disabled by default; API
      error is logged and non-fatal. (14/14 passing, up from 9.)
- [x] 1.7 Verify against a real GitHub repo/issue (not just the test suite):
      ran the Action's render step with `RELEASETWIN_TICKET_WRITE_BACK=true`
      against a real scratch issue
      ([ernestoalejowitt22/ReleaseTwin#130](https://github.com/ernestoalejowitt22/ReleaseTwin/issues/130),
      closed after verification) — confirmed the comment lands with the exact
      expected content for both a passing and a failing case, confirmed an
      unrecognized locator is silently skipped, and confirmed a 404 from a
      nonexistent issue number is logged as a warning without changing the
      run's exit code.

## 2. Documentation

- [x] 2.1 Document the locator convention and the opt-in input in
      `integrations/github-action/README.md`.
- [x] 2.2 Add a "Ticket write-back" section to `docs/ci.md` explaining what it
      does, the supported convention, and that Bitbucket/Azure Boards are
      follow-on work (tasks 3-4 below).

## 3. Bitbucket issues write-back (follow-on)

- [ ] 3.1 Confirm Bitbucket's issue-comment REST endpoint and required App
      password scope against a real Bitbucket repo (Docker/API investigation,
      same standard as `flag-vendor-cookbook`) before writing any code.
- [ ] 3.2 Add locator parsing + comment posting to the Bitbucket Pipe
      (`integrations/bitbucket-pipe`) — this is new code, not an extension of
      an existing render step (the pipe has none today).
- [ ] 3.3 Add an opt-in variable to `pipe.yml`, default off, documented in
      `integrations/bitbucket-pipe/README.md`.
- [ ] 3.4 Verify end to end against a real Bitbucket repo/issue.

## 4. Azure Boards write-back (follow-on)

- [ ] 4.1 Resolve the Open Question in design.md (locator convention, e.g.
      `AB#123`) against a real Azure DevOps org's work-item comment API before
      writing any code.
- [ ] 4.2 Decide where this code lives — there is no existing ReleaseTwin
      Azure Pipelines integration package today, only the raw `docs/ci.md`
      snippet — so this task starts with deciding whether to add one or keep
      this as a documented script snippet.
- [ ] 4.3 Implement + verify against a real Azure DevOps org/work item.
- [ ] 4.4 Document the convention and setup step.

## 5. Verification

- [x] 5.1 `node --test integrations/github-action/render.test.mjs` passes
      with the new tests (14/14).
- [x] 5.2 `openspec validate ticket-evidence-write-back --strict` passes.
- [x] 5.3 Confirm no `src/ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` files
      changed — only `src/ReleaseTwin.Cli` (see 1.0) and `integrations/`
      touched; `dotnet build`/`dotnet test` both green (302/302 tests).
