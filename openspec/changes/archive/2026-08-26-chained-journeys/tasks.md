## 1. Phase 1 — cross-step value capture (foundation)

- [x] 1.1 Design and settle the concrete capture-reference syntax (distinct from `${VAR_NAME}`),
      confirming it can't collide with realistic case-file content (URLs, JSON, headers).
- [x] 1.2 Extend `CaseExecutionContext` (or equivalent) to hold named captures for the duration of
      one case run, cleared between cases.
- [x] 1.3 Extend `core-execution`'s pipeline runner to resolve capture references in a step's
      parameters immediately before executing that step (execution-time, not load-time).
- [x] 1.4 Add capture declaration parsing to `case-loading` (`CaseFileLoader`), validated at load
      time for shape but resolved at execution time.
- [x] 1.5 `HttpRequestOperation`: support declaring a capture from the response (JSON path, header,
      cookie) and support capture references in `url`/`headers`/`body`.
- [x] 1.6 Error handling: referencing a name no earlier step captured fails the case clearly, per
      `value-capture`'s requirement — add tests for step-not-yet-run, step-failed-before-capturing,
      and typo'd-name cases.
- [x] 1.7 Unit tests: capture-then-reference across two `http.request` steps; captures don't leak
      across separate case runs in the same CLI invocation.

## 2. Phase 2 — auth sugar on top of Phase 1

- [x] 2.1 `http.oauth2ClientCredentials` (or similarly named) convenience operation: token-endpoint
      exchange, captures `access_token` (and `expires_in` if useful) the same way any other capture
      works.
- [x] 2.2 Basic auth convenience: username/password params on `http.request` build the `Authorization:
      Basic ...` header automatically.
- [x] 2.3 A real example case demonstrating NAHA's own shape end to end: call `/v1/e2e/login`,
      capture `token`, call `/api/me` with `Authorization: Bearer {token}`.

## 3. Phase 3 — LaunchDarkly flag-proof

- [x] 3.1 New `ReleaseTwin.Adapters.LaunchDarkly` project; `LaunchDarklyFeatureStateController`
      implementing `IFeatureStateController` via LaunchDarkly's REST API.
- [x] 3.2 Adapter credential wiring (API token, project key, environment key) supplied externally,
      matching the existing adapter-sdk convention; clear startup error if incomplete.
- [x] 3.3 Generalize `CliRunner`'s flag-proof adapter selection from "the Azure DevOps adapter
      specifically" to "whichever installed adapter exposes `FeatureStateController`."
- [x] 3.4 Unit tests mirroring the existing Azure DevOps flag-proof tests, against a fake LaunchDarkly
      backend.
- [x] 3.5 Real verification: a flag-proof case against one of NAHA's actual LaunchDarkly flags.
      Unblocked: real LaunchDarkly test-account credentials (API token, project key, environment
      key) now live in AWS Secrets Manager (`releasetwin/e2e/launchdarkly-account`), fetched by a
      new Cypress task the same way the existing real-GitHub-account e2e spec already does. A new
      spec, `web/cypress/e2e/launchdarkly-real-flag-proof.cy.ts`, signs in via real Clerk auth,
      creates a real project, sets those real credentials through the real dashboard
      adapter-credentials form (`hosted-adapter-credentials`), issues a real project token, and
      runs the real CLI — through the hosted-credential-fetch path, not local env vars, so it
      exercises the exact round trip a customer's own CI would take — against the real flag
      `naha.service-catalog-api`. Required one small CLI fix along the way: `LaunchDarklyAdapter`
      was always constructed with its hardcoded demo flag key (`release-proof-feature`); added an
      optional `LAUNCHDARKLY_FLAG_KEY` env var (`CliRunner.cs`) so a real run can point
      `ld.readFeatureFlag` at a different real flag, matching the existing per-adapter-config
      convention. Ran for real: `npm run e2e:ld` — `1 passing`, CLI stdout matched
      `FLAGPROOF LD-REAL-FLAGPROOF-<timestamp> (Passed)`, confirming the real toggle-then-read
      round trip against LaunchDarkly. Flag key deliberately lives in the spec file, not the
      secret, so future tests can target other flags without touching Secrets Manager.

**Addendum (not a task, a flagged gap):** adapter credential setup (Azure DevOps's 5 env vars,
LaunchDarkly's 3, and whatever a future adapter needs) has no customer-facing story — no shared
convention across vendors, no guided setup, no hosted secret storage — and does not generalize
automatically to Bitbucket/Azure/GitHub or any future adapter just because `project-connections`
exists; that feature solves a different problem (display-only repo linking, token never persisted)
and is unrelated to supplying live, reusable execution credentials. See design.md's Risks section
("No customer-facing story for adapter credential setup...") for the full writeup. Deliberately not
scoped as a task here — it deserves its own change/design pass, likely before a third or fourth
adapter is added.

## 4. Phase 4 — UI-automation adapter (large, separate effort)

- [x] 4.1 Evaluate and choose a headless-browser driver (e.g. Playwright) — licensing, footprint,
      .NET support, suitability for a CLI customers install and run in their own CI. Chose
      Microsoft.Playwright (MIT-licensed, official .NET binding, actively maintained by Microsoft):
      confirmed real headless Chromium launch/navigation works via a smoke test. Footprint caveat:
      requires a one-time `playwright install` browser-binary download (~a few hundred MB) in
      whatever environment runs the CLI — a real cost for a customer's CI image, noted as the
      trade-off of this choice rather than solved away.
- [x] 4.2 Design the operation vocabulary (navigate, click, fill, wait-for, assert-visible, and
      whatever else a first real journey needs) and browser-session lifecycle within one case run.
      `ui.navigate`/`ui.click`/`ui.fill`/`ui.waitFor`/`ui.assertVisible`, plus a `ui.closePage`
      cleanup operation (case author declares it in `cleanup:`, same convention as
      `azdo.deleteWorkItem`). One `IPage` per case run, created lazily on first use and stashed on
      `CaseExecutionContext.AdapterState["ui.page"]` — mirrors how `http.request` already stashes
      its last response there.
- [x] 4.3 New `ReleaseTwin.Adapters.Ui` project implementing that vocabulary via `adapter-sdk`'s
      composition contract. Capability `browser:chromium` (matching the placeholder name already
      used as an example in `CaseExecutorTests.cs`). Wired into `CliRunner` as opt-in
      (`RELEASETWIN_UI_ENABLED=1`) rather than unconditional like the HTTP adapter, since launching
      a real browser process is expensive and requires browser binaries to be present.
- [x] 4.4 Integrate with `value-capture` so UI-observed values (e.g. on-page text) are capturable
      like any other adapter's. `text:<selector>` capture source (element `innerText`), same
      `CaptureDeclaration`/`OperationResult.Captures` mechanism Phase 1 already generalized — no
      core changes needed.
- [x] 4.5 Integrate with existing failure classification and cleanup — a failed UI step behaves like
      any other operation's failure. Verified: a failed `ui.assertVisible` classifies as `Product`
      (same as any other assertion failure) and the declared `ui.closePage` cleanup still runs.
- [x] 4.6 A real, complete journey case exercising UI → API → API → a third-party HTTP target (e.g.
      DocuSign's own API) end to end, once Phases 1 and 4's UI pieces both exist. Implemented as
      `examples/cases-ui-journey/cases/example-ui-journey.yaml` (its own directory, not
      `examples/cases/`, since that one's swept unconditionally by `ExampleCaseEndToEndTests` with
      the UI adapter's opt-in flag unset — putting it there made that test capability-gate-fail):
      logs into a real public login form via a real headless browser, captures the on-page
      confirmation text, sends it to a first backend API, chains a second API leg's captured value
      into a third-party target, and asserts the third-party response. Ran for real via
      `RELEASETWIN_UI_ENABLED=1 dotnet src/ReleaseTwin.Cli/bin/Debug/net8.0/ReleaseTwin.Cli.dll
      examples/cases-ui-journey/cases` — `PASS UI-JOURNEY-DEMO-1`. (DocuSign itself substituted
      with public echo services, same substitution pattern as Phase 2's NAHA stand-in — no real
      customer credentials available in this environment.)

## 5. Phase 5 — visual builder + hosted, versioned, pinned journeys

- [x] 5.1 New hosted entities: `Journey` (id, project, name) and `JourneyVersion` (journey id,
      version, YAML content, created-by, created-at) — versions immutable once created. No EF Core /
      migrations in this codebase (single-table DynamoDB design) — implemented as new key prefixes
      (`Journey`/`JourneyVersion` in `Attrs.Keys`), new entities, and new repositories following the
      exact existing `Project`/`ApiToken` pattern; `JourneyVersion` created via a conditional write
      (`attribute_not_exists(PK)`) so a version number is never silently overwritten. No new GSI
      needed — both entities are naturally scoped under their parent's partition key (Journey under
      its Project, JourneyVersion under its Journey), the same shape `Project` already uses under
      `Organization`.
- [x] 5.2 Hosted endpoints: web-session-authenticated create/list/read for the builder
      (`Endpoints/JourneyEndpoints.cs`, `ClerkJwt` scheme, org/project-scoped like every other
      dashboard endpoint); API-token-authenticated fetch-by-id-and-version for the CLI
      (`Endpoints/JourneyFetchEndpoints.cs`, `ApiToken` scheme). `version` is a required route
      segment (`/api/cli/journeys/{journeyId}/versions/{version:int}`) — there is no route that
      resolves to "latest."
- [x] 5.3 Fixture handling for hosted journeys: **not solved, by design's own explicit Non-Goal** —
      confirmed still true after implementation, not silently dropped. A hosted journey step needing
      fixture content resolves it via `RELEASETWIN_FIXTURES_ROOT` (defaulting to `./fixtures`) on
      whatever machine runs the CLI, using the exact same local fixture-resolution/hashing code as a
      locally-loaded case (`CaseFileLoader.ForFixturesRoot`) — i.e. the *pipeline* is hosted, but
      fixture verification remains a local concern for this pass, same as it always was. Real hosted
      fixture storage (blob storage, inline content, etc.) remains an explicit follow-up, not
      invented here.
- [x] 5.4 CLI: a new invocation path (`CliRunner.RunJourneyAsync`, `--journey <id>@<version>` on the
      command line) that fetches a pinned journey version and runs it through the existing
      `case-loading`/pipeline machinery, unchanged apart from the YAML's source. Required refactoring
      `CliRunner.RunAsync`'s adapter-setup/execution-loop body into a shared `RunCoreAsync` that
      either loader (local directory or hosted fetch) feeds into — confirmed behavior-preserving via
      the full existing `CliRunnerTests`/`CliRunnerFlagProofTests` suite (34 tests, all still green)
      before adding new journey-specific tests.
- [x] 5.5 CLI: a fetch failure (network, auth, version not found) is a clear error
      (`JourneyFetchException`/`HttpRequestException` caught and reported, not swallowed), not a
      silent no-op — and a hosted-journey run with no `RELEASETWIN_API_TOKEN` is rejected outright
      rather than silently attempting an unauthenticated fetch.
- [x] 5.6 Dashboard: a visual builder (`web/src/app/journeys/`) — per UX scoping with the user:
      generic operation-name + flat key/value parameters (not per-operation structured forms),
      manual `{{name}}` typing for capture wiring (with a "captured so far" reference list shown per
      step, not auto-insert), and HTTP + UI operations only (the two adapters actually relevant to
      composing a journey). Added one small justified extension beyond flat params: a dedicated
      "Headers" sub-editor per step, since `http.request`'s headers genuinely need a nested map
      (a plain string param can't produce one) and both real example journeys already needed it.
      Composing a pipeline updates a live YAML preview client-side; saving posts it to
      `POST /api/journeys/{id}/versions`.
- [x] 5.7 Dashboard: version history view showing who created each version and when — a plain table
      (version, `CreatedByDisplayName`, `CreatedAt`), per UX scoping (no inline content view for
      this pass).
- [x] 5.8 Unit tests: fetch requires a matching project token (`ATokenFromADifferentProjectCannotFetchTheJourney`);
      an unversioned fetch is rejected (`FetchingWithoutASpecificVersionIsRejected`); two fetches of
      the same version return identical content (`TwoFetchesOfTheSameVersionReturnIdenticalContent`);
      editing after a fetch doesn't alter the already-fetched version
      (`EditingAfterAFetchDoesNotAlterTheAlreadyFetchedVersion`) — plus CLI-side tests for the new
      `RunJourneyAsync` path (fetch success, fetch failure, missing token, invalid fetched YAML).
      17 new tests total across `JourneyServiceTests` (6), `JourneyFetchApiTests` (7), and
      `CliRunnerJourneyTests` (4); full solution suites (hosted: 62, CLI-side: 115) all green.
- [x] 5.9 Real verification: author a journey in the builder using Phase 1–2's capture/auth
      primitives (e.g. NAHA's login-then-call shape), save it, and run it via the CLI's hosted-fetch
      path end to end. Done for real, not simulated: a new Cypress spec
      (`web/cypress/e2e/journey-builder.cy.ts`) signs in via real Clerk auth, creates a real project,
      issues a real API token, composes a two-step login-then-call journey entirely through the
      builder UI (capture a token from one HTTP step, wire it as `Bearer {{token}}` on a second),
      saves it (real `POST` to the hosted API, real DynamoDB-backed in-memory store), then shells out
      to the real CLI via a new `runCliJourney` Cypress task using `--journey <id>@<version>` — the
      actual hosted-fetch path, not a mock. Confirmed passing end to end: `cypress run` green,
      `PASS E2E-LOGIN-THEN-CALL-<timestamp>` in the CLI's real stdout. Screenshots captured
      (`web/cypress/screenshots/journey-builder.cy.ts/`, gitignored) show the composed pipeline and
      its live-generated YAML.
