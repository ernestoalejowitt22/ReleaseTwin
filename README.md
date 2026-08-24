# ReleaseTwin

Release-proof testing for integration-heavy, feature-flagged systems: evidence-linked cases, immutable fixtures, prerequisite ownership, deterministic evidence, and paired known-bad/known-good "flag proof" — the mechanic that actually distinguishes a broken build from a fixed one, not just "green in one environment."

Working name; provisional brand is **Validuo**. See `docs/installation-model.md` for the target deployment model and `docs/the prior product-fit-check.md` for how this was validated against a real production test suite's needs.

## What exists today

- **`ReleaseTwin.Core`** — the execution kernel: ordered pipelines, fixture integrity, prerequisites (three-state: satisfied/not-satisfied/inconclusive), cleanup, retry/timeout, resource-key serialization, failure classification, and flag proof.
- **`ReleaseTwin.AdapterSdk`** — the composition-root pattern that lets adapters plug in without touching the core.
- **`ReleaseTwin.Adapters.AzureDevOps`** — one fixed-shape real adapter (work items, prerequisites, cleanup, variable-group-based flag proof). Two additional toy adapters (`ToyHttp`, `ToyFile`) exist only to stress-test the adapter boundary.
- **`ReleaseTwin.Adapters.Http`** — a vendor-neutral, parameterized adapter: `http.request` + `http.assertJsonPath` let a case test *any* REST API from case-file data alone, no adapter code per target. Requires no credentials to install.
- **`ReleaseTwin.Cli`** — a local CLI that loads YAML case files, composes whichever adapters are configured (HTTP always; Azure DevOps only if its 5 env vars are present), reports pass/fail with a CI-usable exit code, and optionally uploads results to the hosted platform if an API token is configured.
- **`hosted/ReleaseTwin.Hosted.Api`** — a separate, JSON-only .NET API: self-serve signup (Clerk-backed, provider-neutral — not tied to any account on a platform unrelated to ReleaseTwin's own adapters), project/token management, an ingest API, and the data behind a dashboard showing uploaded run history and flag-proof results. **Stage 1, free-only** — no billing exists yet. Execution still happens entirely in your own infra; only report metadata (hashes, pass/fail, classification — never fixture content or secrets) is ever uploaded. See `docs/installation-model.md`.
- **`web/`** — the hosted platform's UI: Next.js, React, Tailwind, and shadcn/ui. Owns the landing page, sign-in (via `@clerk/nextjs`), and the dashboard; talks to `ReleaseTwin.Hosted.Api` server-side only (BFF pattern — the browser never calls the API directly).

## Step by step: running the example

### Prerequisites

- .NET 8 SDK
- Nothing else, for the HTTP example — it needs no credentials at all.
- Optionally, for the Azure DevOps example: an organization, project, PAT with Work Item read/write scope, and a variable group.

### 1. Clone and build

```bash
git clone <this-repo>
cd ReleaseTwin
dotnet build ReleaseTwin.sln
```

### 2. Run the bundled HTTP example (no setup required)

```bash
dotnet run --project src/ReleaseTwin.Cli -- examples/cases
```

This runs every case in `examples/cases/`. `example-http.yaml` is a real HTTP call to a public test API and two real JSONPath assertions — against the live internet, no fake handler — and needs no credentials. `example-claim.yaml` and `example-flag-proof.yaml` both need Azure DevOps: without its 5 environment variables set, they report as failing (`missing-capability:http:azure-devops` / `Ineligible`) rather than crashing or silently vanishing from the output — that's still an honest, non-zero exit code, just not a crash. Output looks like:

```
FAIL CLM-042 (Infrastructure): missing-capability:http:azure-devops
FLAGPROOF FLAGPROOF-DEMO-1 (Ineligible): no installed adapter exposes feature-state control
PASS HTTP-DEMO-1
1 passed, 2 failed
```

(If you *do* set the Azure DevOps variables below, all three examples pass together.)

A non-zero exit code means at least one case failed — safe to wire directly into a CI step (`dotnet run --project src/ReleaseTwin.Cli -- cases/ || exit 1`).

### 3. (Optional) Set Azure DevOps credentials

Never commit these. If any of the 5 are set, all 5 must be — a partial set is a startup error, not a silent skip:

```bash
export AZDO_ORG=your-org
export AZDO_PROJECT=YourProject
export AZDO_PAT=your-personal-access-token
export AZDO_AREA_PATH='YourProject\YourArea'
export AZDO_VARIABLE_GROUP_ID=1
```

### 4. Write your own case

**Testing a REST API** (no new adapter code needed — see `examples/cases/example-http.yaml`):

```yaml
id: MY-CASE-1
oracle:
  locator: tickets/MY-CASE-1
fixture:
  locator: my-fixture.json       # resolved relative to a fixtures/ directory next to cases/
  sha256: <sha256 of the fixture file>
pipeline:
  - operation: http.request
    with:
      method: POST
      url: ${API_BASE_URL}/orders       # ${ENV_VAR} resolved at load time — never commit real URLs/secrets
      headers:
        Authorization: Bearer ${API_TOKEN}
      body:
        productId: 123
  - operation: http.assertJsonPath
    with:
      path: $.status
      expected: confirmed
```

**Testing against Azure DevOps** (fixed-shape operations, see `examples/cases/example-claim.yaml`):

```yaml
id: MY-CASE-2
oracle:
  locator: tickets/MY-CASE-2
fixture:
  locator: my-fixture.json
  sha256: <sha256 of the fixture file>
requires:
  - http:azure-devops
preconditions:
  - check: azdo.areaPathExists
    owner: QA claims fixtures
pipeline:
  - operation: azdo.createWorkItem
  - operation: azdo.getWorkItem
cleanup:
  - operation: azdo.deleteWorkItem
resource_key: 'TeamProject\Area'   # optional — serializes cases sharing this key
```

Available operation/precondition/cleanup names today:
- **Generic HTTP** (any REST API): `http.request`, `http.assertJsonPath`.
- **Azure DevOps** (fixed-shape): `azdo.createWorkItem`, `azdo.getWorkItem`, `azdo.transitionWorkItemState`, `azdo.areaPathExists`, `azdo.deleteWorkItem`, `azdo.readFeatureVariable`.

**Known limitation:** Azure DevOps's operations still take no per-case parameters — a case selects *which* Azure DevOps operations run, not what data they act on. The HTTP adapter doesn't have this limitation. See "What's not built yet" below.

**Flag proof** (paired known-bad/known-good run, see `examples/cases/example-flag-proof.yaml`): add a `flag_proof` block to any case that also has Azure DevOps configured, and the CLI runs it twice — once with the feature toggled off, once on — reporting a single discriminating outcome instead of a plain pass/fail:

```yaml
flag_proof:
  feature_key: release-proof-feature   # the variable-group variable to toggle
  build_identity: build-123            # an identifier for this run, carried through the report
```

Output looks like `FLAGPROOF FLAGPROOF-DEMO-1 (Passed)` — or `WeakOracle`/`BothFailed`/`Inverted`/`Ineligible` when the case's own pipeline can't actually tell known-bad from known-good, or when no installed adapter exposes feature-state control. Only Azure DevOps (its variable-group `IFeatureStateController`) can drive this today.

### Running the automated test suite

```bash
dotnet test ReleaseTwin.sln
```

68 tests, all offline (fake HTTP responses) except two integration tests that skip automatically unless `AZDO_ORG`/`AZDO_PROJECT`/`AZDO_PAT`/`AZDO_AREA_PATH` are set to a real sandbox org.

The hosted platform is a separate solution with its own test suite:

```bash
cd hosted
dotnet test ReleaseTwin.Hosted.slnx
```

31 tests, all against a real (in-memory or SQLite) database and the real ASP.NET Core pipeline — no live Clerk application is needed to run them. The frontend (`web/`) has no automated tests yet — see `docs/installation-model.md`'s note on why Playwright/Cypress wasn't added speculatively.

## Self-serve signup (Stage 1, free-only)

The hosted platform is real but **not yet offered to anyone** — no Clerk application has been registered on the operator side, which is a one-time manual setup step outside this repo. As of `hosted-react-frontend`, the hosted platform is **two services**, not one: a JSON-only .NET API and a Next.js/React/Tailwind frontend that owns all UI (landing page, sign-in, dashboard) and talks to the API server-side only (BFF pattern — the browser never calls the .NET API directly, so there's nothing to configure for CORS).

**1. The .NET API** — its only Clerk-related job is verifying session JWTs against Clerk's public JWKS, so it needs just the Clerk *domain*, not a Client ID/Secret (those are Next.js-side, see below):

```bash
cd hosted
export Clerk__Domain=your-app.clerk.accounts.dev
export Database__SqlitePath=/path/to/local.db   # or ConnectionStrings__Hosted for Postgres
dotnet run --project ReleaseTwin.Hosted.Api
```

**2. The Next.js frontend** — uses `@clerk/nextjs`'s own Publishable/Secret key pair (Clerk Dashboard → API Keys — a *different* pair from any OAuth Application credentials):

```bash
cd web
cp .env.local.example .env.local   # then fill in real values
npm install
npm run dev
```

`.env.local` needs `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY`, and `RELEASETWIN_API_URL` (pointing at the .NET API from step 1 — same naming convention the CLI already uses).

**Optional — connecting a project to a GitHub repo** (`project-connections`, purely a display label, no credential is ever stored): register a *separate* GitHub OAuth App for this (distinct from Clerk entirely), with its authorization callback URL pointed at the **Next.js** app: `<your web app URL>/connect/github/callback`, and its scope limited to `read:user` (public repos only — this deliberately does not request the `repo` scope, so it can never see private repos; see `docs/installation-model.md` if that needs revisiting). These three go on the **.NET API**, since that's what actually talks to GitHub:

```bash
export GitHubConnection__ClientId=your-github-oauth-app-client-id
export GitHubConnection__ClientSecret=your-github-oauth-app-client-secret
export GitHubConnection__CallbackUrl=http://localhost:3000/connect/github/callback
```

Without these three set, "Connect GitHub" on the dashboard just says connections aren't configured yet — everything else keeps working.

Then, from the CLI/library solution root:

```bash
export RELEASETWIN_API_TOKEN=<token issued from the hosted dashboard>
export RELEASETWIN_API_URL=http://localhost:5000   # wherever the hosted API is actually running
dotnet run --project src/ReleaseTwin.Cli -- examples/cases
```

Uploads happen automatically after each case; a failed upload prints a warning but never changes the case's own pass/fail result or the CLI's exit code.

## What's not built yet

Deliberately deferred, not forgotten — each was a scoped decision, not an oversight:

- **Packaging/distribution** — no npm, NuGet, Docker image, or GitHub Action for the CLI. Today it's `dotnet build` from source only.
- **Config-driven adapter selection** — the CLI still decides which adapters to install in code (HTTP always, Azure DevOps conditionally), not from a config file naming arbitrary adapters.
- **Azure DevOps operation parameters** — its operations are still fixed-shape; only the HTTP adapter is data-driven from case files.
- **A non-REST adapter** — the HTTP adapter covers anything with a REST surface; a message queue, database, or vendor SDK without one still needs bespoke adapter code.
- **A generic (non-Azure-DevOps) flag-proof mechanism** — the CLI can now run flag-proof pairs end-to-end (a case declares `flag_proof: { feature_key, build_identity }` and the CLI reports `FLAGPROOF <id> (<outcome>)`), but only against Azure DevOps's variable-group `IFeatureStateController`; a flag source that isn't Azure DevOps (LaunchDarkly, a config service, a REST endpoint) still needs a new implementation.
- **External-check connector (Playwright)** — visual/browser evidence isn't wired in.
- **Billing** — the hosted platform (above) is Stage 1 only: no Stripe integration, no paid tiers, no usage enforcement.
- **A registered Clerk application** — the hosted platform's sign-in code is built and tested (Clerk-backed, provider-neutral), but no application has actually been registered yet, so it isn't offered to anyone outside this repo.
- **Three-state prerequisites for other checks** — only Azure DevOps's `areaPathExists` check uses the inconclusive state; it's available to any adapter but nothing else has needed it yet.

## How far is this from commercial use?

Short answer: **technically promising, commercially unvalidated.** Two different bars, and only one has been cleared.

### Cleared: the technical go/no-go

The the initial design brief's own criterion was specific — an adapter unrelated to the original the prior product use case must plug in "without modifying the core model or runner." That happened twice now: the Azure DevOps adapter, and then the generic HTTP adapter, both shipped without *unplanned* changes to `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` (two deliberate, tracked core evolutions did happen — three-state prerequisites, then operation parameters — each decided mid-implementation for a concrete reason, not forced blindly). That's real, repeated evidence the core/adapter boundary is sound, not just a story.

**The Tier 1/Tier 2 usage gap is now partially closed too** (see `docs/customer-pilot-guide.md`): a prospect with a REST API can author a real case testing real business behavior today, not just watch a fixed demo — proven end to end against a live public API, not a fake handler.

### Not yet started: the commercial go/no-go

The same assessment is explicit that this isn't enough on its own: *"Go if an unrelated second adapter can be implemented without ordinary changes to the core **and** design partners value the release-proof workflow."* The second half hasn't been touched:

- **No design partners contacted yet.** Nothing about willingness to pay, which reports matter, or whether "release-proof" as a concept resonates has been tested with an actual outside user.
- **No pricing validated.** the initial design brief numbers ($149/mo Team, $499/mo Growth, $5-15k pilots) are explicitly labeled hypotheses, not offers anyone has seen.
- **No legal/entity work started.** Naming (Validuo, provisional), trademark search, incorporation, IP ownership documentation (relevant given this was extracted conceptually from a the prior product-adjacent codebase, deliberately clean-slate built to avoid that entanglement) — none of it done.
- **Not actually offered to anyone yet.** Self-serve signup/dashboard code exists and is tested (`hosted-self-serve-platform`, `clerk-registration`, `hosted-react-frontend`), but no Clerk application has been registered — a real one-time setup step, not a code gap. Even once that's done, running the CLI itself still means cloning source and having the .NET SDK — no packaging exists.

### Realistic framing

This is pre-pilot. If someone asked "can I buy this today," the honest answer is no — there's nothing to hand a paying customer, and the workflow's actual value hasn't been tested against a real business problem outside this project. The right next milestone, per the initial design brief own plan, is a paid design-partner pilot (their suggested range: $5,000–$15,000, six to eight weeks, one real workflow) — which is a sales/business-development step, not an engineering one. The engineering foundation to support that pilot conversation now exists; the conversation itself hasn't happened yet.
