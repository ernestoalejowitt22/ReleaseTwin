## Context

See proposal.md for why. Two existing facts shape everything here:
- `case-loading`'s environment-variable interpolation (`${VAR_NAME}`) resolves once, at load time,
  before any case runs — `CaseFileLoader` parses the whole batch up front.
- `core-execution`'s pipeline already runs ordered operations from potentially multiple installed
  adapters against one `CaseExecutionContext`, and `HttpRequestOperation` already stashes
  `http.lastStatusCode`/`http.lastBody` on that context per-step — the mechanism this change extends
  is already half-built, just not exposed to later steps or other adapters.

## Goals / Non-Goals

**Goals:**
- One case file can express a multi-step flow (login-then-call, create-then-reference, a UI leg
  feeding an API leg) without external orchestration.
- The capture/reference primitive is adapter-agnostic from day one, even though Phase 1 only ships
  an HTTP producer/consumer — `ui-adapter` (Phase 4) must be able to plug into the same mechanism
  without changing it.
- Flag-proof works against LaunchDarkly, not only Azure DevOps.
- A hosted journey run is exactly as reproducible as a pinned local case file — running the same
  version twice never silently executes different content.

**Non-Goals:**
- Phase 5 does not solve fixture storage for hosted journeys — see Risks below; a journey step that
  needs fixture content (like `example-http.json` today) has nowhere hosted to live yet, and this
  change does not invent that storage. Real fixture handling belongs in Phase 5's own follow-up
  design once the journey-storage/versioning mechanism itself is proven.
- Phase 5 does not build any offline/air-gapped fallback — a hosted-journey run requires network
  access to the hosted API, same as an upload already does today.
- Not building a general scripting/expression language for captured values (no transforms, no
  arithmetic) — a capture is a value, referenced verbatim; anything fancier is future scope if a
  real case ever needs it.
- Not attempting OAuth2 authorization-code/PKCE (interactive) flows — client-credentials (machine-
  to-machine) and login-endpoint-returns-a-token (NAHA's own shape) cover the realistic automated-
  testing cases; a real interactive human login isn't something a CI-run CLI should be doing anyway.
- Phase 4 (UI adapter) does not attempt to also solve journeys spanning a real third-party UI (e.g.
  driving DocuSign's own signing page) — only this app's/customer's own UI plus API legs, including
  third-party APIs (DocuSign's REST API is just another HTTP-adapter target once Phase 1 exists).

## Decisions

**Captured-value references use different syntax from `${VAR_NAME}`, resolved at a different time.**
Environment variables are known before any case runs and resolve once, at load. Captured values
don't exist until an earlier step has actually executed, so they must resolve per-step, at pipeline-
execution time — reusing `${...}` for both would conflate two different resolution phases and make
it ambiguous, from reading a case file, when a given reference actually resolves. Concrete syntax
(e.g. `{{captureName}}`) is a naming detail to settle during Phase 1 implementation, not a design
question — the load-time/execution-time split is the real decision.

**Captures are scoped to a single case run, never shared across cases or persisted.** Matches
`value-capture`'s own "do not persist beyond a single case run" requirement — this keeps cases
independently runnable/parallelizable (`core-execution`'s existing "resource-key serialization"
requirement already assumes cases can interleave) and avoids a whole class of ordering bugs a
shared/global capture store would invite.

**OAuth2 client-credentials and Basic auth are convenience sugar on `http.request`, not new
adapters.** Both are just HTTP requests with standardized shapes; building them as thin conveniences
that produce a capture (client-credentials) or a header (Basic) keeps them inside the existing
adapter rather than justifying a new one.

**LaunchDarkly is a new adapter implementing the existing `IFeatureStateController`, not a change to
core.** `FlagProofRunner` already depends on the interface, not on Azure DevOps concretely — this is
genuinely additive. The one real code change outside the new adapter is `CliRunner`'s flag-proof
wiring, which today only ever asks the Azure DevOps adapter for a `FeatureStateController`; it needs
to ask "whichever installed adapter exposes one," matching what the `cli-runner` spec's requirement
text already promises (it never named Azure DevOps specifically).

**The UI adapter is scoped last, deliberately, and treated as its own large effort.** Everything in
Phases 1–3 extends patterns already in the codebase (an existing interface, an existing operation,
an existing per-step context). The UI adapter introduces a genuinely new category: a stateful
browser session across steps, a new dependency (a headless-browser driver — not yet chosen), and an
operation vocabulary this codebase has no precedent for. It benefits from Phase 1 existing first
(so a UI-captured value can flow into API steps from day one) but doesn't block on Phases 2–3.

**Journey versions are immutable and pinned by ID; there is no "latest" a CI run can implicitly
track.** This is the direct answer to the trust-boundary shift `hosted-journeys` introduces: a case
pipeline can already make arbitrary HTTP requests with arbitrary headers (via interpolation, likely
including real secrets), so "the CLI runs whatever's currently saved" would mean a dashboard edit —
or a compromised dashboard account — changes what a customer's CI executes without that customer's
CI configuration changing at all. Requiring an explicit version pin (the same shape as pinning a
dependency version or a container image digest) means a customer's own CI config is the only thing
that can change what runs; a journey edit only takes effect when the customer deliberately bumps
the pin.

**No signing/checksum layer beyond version immutability, for this pass.** TLS already protects the
fetch in transit; the remaining risk is a compromised or buggy hosted backend serving different
content for the same version ID, which immutability plus normal database integrity should already
prevent in practice. A content-hash the customer could additionally pin (defense-in-depth against
an implementation bug breaking that immutability guarantee, similar to a lockfile's integrity hash)
is a reasonable hardening step, but not required to satisfy "pinned and auditable" — noted as a
candidate follow-up, not deferred silently.

## Risks / Trade-offs

- **Fixture content has nowhere hosted to live.** A journey step needing fixture content (the way
  local cases reference `examples/fixtures/*.json`) can't reference a customer's local filesystem
  once the journey itself is hosted and fetched remotely. → Not solved here (see Non-Goals) — Phase
  5's own follow-up design needs to decide whether fixtures become hosted blobs, inline content in
  the journey version itself, or something else, before a hosted journey involving fixture-bearing
  operations can actually run.
- **This is the first hosted capability the CLI executes rather than only reports to.** Distinct in
  kind from every other hosted capability so far. → Mitigated by the immutable-version-pin decision
  above, but worth re-stating plainly: this is a real, deliberate expansion of what the hosted
  platform is trusted to do, not a small addition.

- **Capture syntax colliding with existing case-file content.** Whatever syntax is chosen must not
  collide with legitimate literal strings a case might already contain. → Mitigate by choosing a
  syntax deliberately unlikely to appear in real URLs/JSON/headers (to be finalized in Phase 1) and
  by making an unresolvable reference a hard load/execution error (already required above), not a
  silent literal pass-through.
- **A capture step failing before producing its value.** If a step declares a capture but fails
  before reaching the point of extracting it, later steps referencing that name must fail clearly
  (per `value-capture`'s "referencing an unavailable capture" requirement) rather than receive a
  stale or default value.
- **Choice of headless-browser driver for Phase 4 is unresolved.** Not decided here — a real
  evaluation (Playwright vs. alternatives, licensing, footprint in a CLI a customer installs and
  runs in their own CI) belongs in Phase 4's own design work, not guessed at now.
- **No customer-facing story for adapter credential setup, and it doesn't generalize today.** Each
  adapter that actually executes operations (Azure DevOps, now LaunchDarkly, eventually a real
  GitHub adapter if one is ever added for execution rather than just linking) invents its own flat
  list of required environment variables (`AZDO_ORG`/`AZDO_PROJECT`/`AZDO_PAT`/`AZDO_AREA_PATH`/
  `AZDO_VARIABLE_GROUP_ID`; `LAUNCHDARKLY_API_TOKEN`/`LAUNCHDARKLY_PROJECT_KEY`/
  `LAUNCHDARKLY_ENVIRONMENT_KEY`), which `CliRunner` reads directly from the process environment. A
  customer discovers what's required only from documentation or from `CliRunner`'s own
  partial-config error message (which does at least name the specific missing keys) — there is no
  guided setup, no hosted secret storage, and no shared shape across vendors to build one against.
  This is a **distinct problem from `project-connections`**, which is unrelated despite the
  surface-level similarity: `project-connections`'s GitHub OAuth flow is dashboard-only *display*
  metadata (which repo a project is labeled with) and by design never persists a token anywhere —
  it grants the hosted platform nothing to execute with. Adapter credentials are the opposite: they
  must be usable, repeatedly, wherever the customer's own CI runs the CLI, since flag-proof and any
  real `http.request` target need a live credential at execution time, not a one-time OAuth grant
  the hosted platform never sees again. `project-connections`'s `Connection.Provider` being a plain
  string (not an enum) does mean *linking* a project to a Bitbucket or Azure DevOps repo needs no
  schema change later — but that says nothing about whether a Bitbucket or Azure-DevOps-flavored
  adapter's own execution credentials would be easy for a customer to set up; each new vendor still
  means a new bespoke env-var list invented from scratch, and a customer running the CLI in, say,
  GitHub Actions vs. Bitbucket Pipelines vs. Azure Pipelines has a different idiomatic way to inject
  secrets that this design does nothing to standardize. → Not solved here. Worth a dedicated design
  pass (own change, not a Phase 3 afterthought) before a third or fourth adapter makes the
  per-adapter-env-var-list pattern more painful: candidates include a documented common secret-name
  convention, a hosted "connect your CI credentials" flow analogous to `project-connections` but for
  execution rather than display (with the token actually usable later, unlike `project-connections`'s
  explicit never-persist requirement — a materially different trust model that would need its own
  spec), or simply better first-run documentation/tooling (e.g. `releasetwin doctor` reporting which
  adapters are configured and what's missing) without inventing hosted secret storage at all.

## Migration Plan

None of the five phases requires a data migration. Phase 1 is purely additive to case-file syntax
and pipeline execution (existing case files without captures are unaffected). Phases 2–4 are new
adapters/operations, opt-in by whether a case file uses them. Phase 5 adds new hosted entities
(journeys, immutable versions) with no relationship to existing ones — opt-in by whether a customer
uses the visual builder at all.
