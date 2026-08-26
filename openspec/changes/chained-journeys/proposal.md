## Why

Dogfooding against a real customer-shaped API (NAHA) surfaced two gaps that turn out to be the same
missing primitive at different scales. NAHA's real endpoints need "log in, then call" — the single
most common real-world API test shape — and the HTTP adapter has no way to capture a value from one
step's response and use it in a later step's request. The same gap is why "journeys" (UI → API →
API → a third party like DocuSign) aren't expressible today: `core-execution`'s ordered pipeline and
`adapter-sdk`'s "multiple adapters compose without conflict" already let one case mix operations from
different adapters — what's missing is purely the wiring between steps. Separately, NAHA's real
feature flags are LaunchDarkly, not Azure DevOps variable groups, so flag-proof — the stated
differentiator — doesn't reach the one real customer we have at all today.

This captures all of it as one plan, phased, so none of it gets lost even though it will land
incrementally.

## What Changes

**Phase 1 — cross-step variable capture (foundation for everything else).**
- A step can declare a capture: pull a value out of its own result (JSON field via JSONPath, a
  response header, a cookie) and bind it to a name.
- Any later step in the same case, from any adapter, can reference that name in its own parameters
  — the same interpolation mechanism `case-loading` already uses for environment variables, extended
  to also resolve captured values.
- `HttpRequestOperation` gains the ability to both produce captures (from its response) and consume
  them (in URL/headers/body).

**Phase 2 — standardized auth as sugar on top of Phase 1, not new infrastructure.**
- Login-then-call (NAHA's own `/v1/e2e/login` shape: call an endpoint, capture a token field, use it
  as `Authorization: Bearer {token}` in later steps) needs no new operation at all once Phase 1
  exists — it's just a two-step case.
- OAuth2 client-credentials grant gets one convenience operation (`http.oauth2ClientCredentials` or
  similar) that performs the standard token-endpoint exchange and captures `access_token`, since it's
  common enough and standardized enough to be worth not hand-rolling every time.
- Basic auth gets a small header-building convenience (username/password params → the base64-encoded
  header), since encoding it by hand in a case file is real but pointless friction.

**Phase 3 — LaunchDarkly as a second flag-proof-capable adapter.**
- A new `LaunchDarklyFeatureStateController` implementing the existing `IFeatureStateController`
  (`SetStateAsync(featureKey, enabled, ct)`) via LaunchDarkly's REST API.
- `CliRunner`'s flag-proof wiring, which currently only ever looks at the installed Azure DevOps
  adapter for feature-state control, generalizes to "whichever installed adapter exposes one" — no
  spec change needed here, since `cli-runner`'s own requirement text is already adapter-agnostic
  ("no installed adapter exposes feature-state control").

**Phase 4 — a UI-automation adapter (the largest, most separate piece).**
- A new adapter in the same family as the HTTP/Azure DevOps ones, but a materially bigger build: a
  headless-browser dependency, browser-context/session lifecycle, and a new operation vocabulary
  (navigate, click, fill, wait-for, assert-visible, and similar).
- This is what makes the "UI" leg of a journey (e.g. UI → API → API → DocuSign) possible at all — the
  API/API/DocuSign legs are already just HTTP-adapter steps chained via Phase 1.

**Phase 5 — a visual journey builder, journeys stored hosted, fetched by the CLI at run time.**
- The dashboard gains a visual builder for authoring a journey (a pipeline of steps across whatever
  adapters, with captures wired between them) — a natural fit given Phase 1's capture/reference
  model is otherwise something a case author writes by hand.
- Journeys are stored hosted, not only as local case files: this crosses a boundary `ingest-api`
  has held deliberately since it exists — the hosted platform has never been a source of anything a
  CLI *executes*, only a sink for uploaded results. A saved journey is versioned and immutable once
  created; editing produces a new version, never an in-place mutation.
- The CLI fetches a *specific, pinned* journey version at run time (authenticated by the project's
  token) rather than "whatever is currently saved" — a customer's CI configuration references an
  explicit version, the same way a dependency lockfile pins a version, so a re-run of the same CI
  job always executes the same journey unless the customer deliberately bumps the pin.
- The fetched YAML is parsed by the exact same `case-loading` model Phase 1–4 already extended —
  only the *source* of the YAML changes (a hosted fetch instead of a local file), not the format or
  the pipeline/execution model.

## Capabilities

### New Capabilities
- `value-capture`: cross-step capture and reference of values (JSON field, header, cookie) within a
  single case run, usable by any adapter's operations.
- `launchdarkly-adapter`: a LaunchDarkly-backed `IFeatureStateController`, making flag-proof usable
  against LaunchDarkly-flagged systems.
- `ui-adapter`: browser-driven operations (navigate, click, fill, wait, assert) as a new adapter.
- `hosted-journeys`: hosted storage of versioned, immutable journey definitions, and an
  authenticated fetch endpoint the CLI uses to retrieve one pinned version at run time.

### Modified Capabilities
- `case-loading`: parameter interpolation extends beyond environment variables to also resolve
  captured values from earlier steps.
- `http-adapter`: `http.request` gains the ability to declare a capture from its own response, and to
  consume captured values (and, for Phase 2, the OAuth2 client-credentials and basic-auth
  conveniences).
- `dashboard`: gains a visual builder for authoring and saving a journey to `hosted-journeys`.
- `cli-runner`: gains the ability to run a pinned hosted journey (by ID and version) in place of, or
  alongside, a local cases directory.

## Impact

- `src/ReleaseTwin.Core`: pipeline execution needs to thread captured values between steps
  (`core-execution`'s ordered-pipeline machinery).
- `src/ReleaseTwin.Cli/CaseLoading`: interpolation logic.
- `src/ReleaseTwin.Adapters.Http`: `HttpRequestOperation` capture/consume support, new convenience
  operations.
- New `src/ReleaseTwin.Adapters.LaunchDarkly` project; `src/ReleaseTwin.Cli/CliRunner.cs`'s flag-proof
  adapter-selection logic.
- New `src/ReleaseTwin.Adapters.Ui` (or similar) project — a new dependency (a headless browser
  driver) not currently in this codebase at all.
- New hosted entities for journeys and their immutable versions, plus a token-authenticated fetch
  endpoint (`hosted/ReleaseTwin.Hosted.Api`) — the first hosted capability that hands the CLI
  something to *execute*, distinct in kind from `ingest-api`'s existing upload-only direction.
- `web/`: a new visual builder UI in the dashboard.
- Phases are independently shippable in order — 2 and 3 both depend on 1; 4 depends on nothing here
  but is far larger than 1–3 combined; 5 depends on 1 (captures are what make the builder's
  step-wiring meaningful) and is independent of 3–4.
