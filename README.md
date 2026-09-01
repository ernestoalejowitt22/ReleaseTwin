# ReleaseTwin

Release-proof testing for integration-heavy, feature-flagged systems: evidence-linked cases, immutable fixtures, prerequisite ownership, deterministic evidence, and paired known-bad/known-good "flag proof" — the mechanic that actually distinguishes a broken build from a fixed one, not just "green in one environment."

See `docs/installation-model.md` for the target deployment model.

**New here?** [docs/quickstart.md](docs/quickstart.md) — test your first API in 10 minutes with `docker run … init`, no account or clone.

## What exists today

- **`ReleaseTwin.Core`** — the execution kernel: ordered pipelines, fixture integrity, prerequisites (three-state: satisfied/not-satisfied/inconclusive), cleanup, retry/timeout, resource-key serialization, failure classification, and flag proof.
- **`ReleaseTwin.AdapterSdk`** — the composition-root pattern that lets adapters plug in without touching the core.
- **`ReleaseTwin.Adapters.AzureDevOps`** — one fixed-shape real adapter (work items, prerequisites, cleanup, variable-group-based flag proof). Two additional toy adapters (`ToyHttp`, `ToyFile`) exist only to stress-test the adapter boundary.
- **`ReleaseTwin.Adapters.Http`** — a vendor-neutral, parameterized adapter: `http.request` + `http.assertJsonPath` let a case test *any* REST API from case-file data alone, no adapter code per target. Requires no credentials to install.
- **`ReleaseTwin.Cli`** — a local CLI that loads YAML case files, composes adapters from an optional `releasetwin.yaml` (or auto-detects them from present credentials), reports pass/fail with a CI-usable exit code, and optionally uploads results to the hosted platform if an API token is configured.
- **`hosted/ReleaseTwin.Hosted.Api`** — a separate, JSON-only .NET API: self-serve signup (Clerk-backed, provider-neutral — not tied to any account on a platform unrelated to ReleaseTwin's own adapters), **team membership** (invite teammates by email; `admin` / `member` / `viewer` roles; a user can belong to several organizations and switch between them), project/token management, an ingest API, and the data behind a dashboard showing uploaded run history and flag-proof results. Paid Team upgrades go through **Polar (Merchant of Record)**; per-project pricing, where the billable quantity is the **project count only** — adding teammates never changes the bill. Execution still happens entirely in your own infra; only report metadata (hashes, pass/fail, classification — never fixture content or secrets) is uploaded by default. You can optionally opt in (per project, Team tier) to also upload a per-run **evidence document** — request/response summaries, assertion detail, screenshots — which is redacted in your own CLI before upload (auth headers, credential-shaped fields, and resolved secrets stripped automatically, plus your own allowlist/denylist rules) and rendered as a per-report drill-down on the dashboard. Two further **Team-tier**, opt-in, feature-flagged additions: outbound **run-failure notifications** (a Slack or generic-webhook alert on a failed run or a flag proof that didn't discriminate — carrying only the result and a dashboard link) and revocable, read-only **evidence share links** (a per-run link that renders that run's already-redacted evidence to someone with no account, and nothing else). See `docs/installation-model.md`.
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
FLAGPROOF FLAGPROOF-DEMO-1 (Ineligible): no installed adapter exposes feature-state control and the case declares no flag_proof.control
PASS HTTP-DEMO-1
1 passed, 2 failed
```

### 2b. Or run it via Docker (no .NET SDK required)

The CLI is also published as a container image — useful for CI systems (or machines) without the .NET SDK installed:

```bash
docker pull ghcr.io/ernestoalejowitt22/releasetwin/cli:<version>   # pin a version, e.g. 0.1.0 — avoid :latest in CI
docker run --rm \
  -v $(pwd)/examples:/workspace:ro \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:<version>
```

The container expects the same layout the CLI already assumes: a `cases/` directory with a sibling `fixtures/` directory, both under whatever host path you mount to `/workspace`. Running with no arguments executes `/workspace/cases`; pass a different path as an argument to override it (`docker run ... ghcr.io/ernestoalejowitt22/releasetwin/cli:<version> /workspace/some-other-dir`). The mount is read-only — the CLI only ever reads case/fixture files, never writes to them.

Credentials/env vars work the same way as the plain CLI, just passed into the container: either `--env-file .env` (handy if you already keep a local `.env` next to your case files) or `-e SOME_VAR` (bare, no `=value` — passes through the host's own value, useful when a CI system already exports secrets as environment variables). Exit code behavior is unchanged — `docker run` propagates the container's exit code, so the same `... || exit 1`-style CI gating works without adaptation.

**Pin a version tag in CI.** `:latest` is published for convenience/local smoke-checking, but an unannounced image update silently changing your CI gate would defeat the point of a reliable regression check — pin an explicit version (e.g. `:0.1.0`) in any real CI pipeline.

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
release: "4.2"                    # optional — groups this case in the hosted release rollup
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

**Flag proof** (paired known-bad/known-good run, see `examples/cases/example-flag-proof.yaml`): add a `flag_proof` block to any case, and the CLI runs it twice — once with the feature toggled off, once on — reporting a single discriminating outcome instead of a plain pass/fail:

```yaml
flag_proof:
  feature_key: release-proof-feature   # the flag to toggle
  build_identity: build-123            # an identifier for this run, carried through the report
```

The toggle is driven by whichever installed adapter exposes feature-state control (Azure DevOps's variable group, or LaunchDarkly). If none does — or the flag lives in a system with no adapter — add a `control` block and the always-present HTTP adapter flips it over REST (see `examples/cases-flag-proof-http/example-flag-proof-http.yaml` and `docs/flag-proof.md`):

```yaml
flag_proof:
  feature_key: checkout-v2
  build_identity: orders@2f9c1a
  control:
    method: PUT
    url: ${FLAGS_API}/flags/{{featureKey}}      # {{featureKey}} / {{state}} / {{enabled}} are per-leg
    headers: { Authorization: "Bearer ${FLAGS_TOKEN}" }   # credentials only via ${ENV} / hosted secrets
    body: '{ "state": "{{state}}" }'
    known_bad_when: disabled                    # or `enabled` to invert polarity
```

Output looks like `FLAGPROOF FLAGPROOF-DEMO-1 (Passed)` — or `WeakOracle`/`BothFailed`/`Inverted` when the case's own pipeline can't tell known-bad from known-good, `Ineligible` when nothing can drive the toggle, or `ControlFailed` when the `control` request itself errors.

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

37 tests, all against the real ASP.NET Core pipeline — no live Clerk application is needed to run them. As of `usage-metering`, persistence is DynamoDB, not EF Core/Postgres/SQLite: most tests run against an in-memory fake of the single hosted table, with a smaller `Category=Integration`-tagged set (skips automatically unless `DYNAMODB_LOCAL_URL` is set) exercising real DynamoDB semantics via DynamoDB Local — `cd hosted && docker compose up -d`.

The frontend (`web/`) has real Cypress e2e coverage as of `web-cypress-e2e` — one spec automating the actual sign-in → dashboard → create project → issue token → sign out walkthrough against a **real, live Clerk instance** (not mocked). This spec is local-only — it needs live Clerk credentials, so it isn't wired into CI. Everything else *is*: `.github/workflows/ci.yml` builds and runs `dotnet test` for the CLI solution (installing Chromium for the Playwright UI tests) on every push to `main` and every PR, `.github/workflows/hosted-ci.yml` does the same for `hosted/`, and `.github/workflows/release.yml` tests + builds + pushes the multi-arch CLI image on `v*.*.*` tags. To run the Cypress spec:

```bash
cd web
cp cypress.env.json.example cypress.env.json   # E2E_TEST_USER_EMAIL — must use Clerk's "+clerk_test@" convention
export Clerk__Domain=your-app.clerk.accounts.dev   # same value ReleaseTwin.Hosted.Api needs, see below
npm run e2e
```

`npm run e2e` boots both services (`dotnet run` for the API, `next dev` for the frontend) and tears them down after. Prerequisites: password sign-in enabled as an auth method on the Clerk instance (verified — the scripted test user still needs a password set at creation time even though it signs in passwordlessly via `email_code`), and the same Clerk/`web/.env.local` credentials already required for `web/` generally (see "Self-serve signup" below).

## Self-serve signup

The hosted platform is real and **deployed** — the .NET API runs as an AWS Lambda (Function URL) and the `web/` frontend is on Vercel, both auto-deploying from `main`. A **production Clerk instance** is registered and wired to it. What hasn't happened is *going public*: self-serve sign-up is not linked, announced, or offered to anyone outside this repo yet, and no outside user has been invited — that is a business decision, not a code or setup gap. As of `hosted-react-frontend`, the hosted platform is **two services**, not one: a JSON-only .NET API and a Next.js/React/Tailwind frontend that owns all UI (landing page, sign-in, dashboard) and talks to the API server-side only (BFF pattern — the browser never calls the .NET API directly, so there's nothing to configure for CORS).

**1. The .NET API** — its only Clerk-related job is verifying session JWTs against Clerk's public JWKS, so it needs just the Clerk *domain*, not a Client ID/Secret (those are Next.js-side, see below):

```bash
cd hosted
export Clerk__Domain=your-app.clerk.accounts.dev
docker compose up -d                                 # DynamoDB Local, for local dev
export Aws__DynamoDb__ServiceUrl=http://localhost:8000
dotnet run --project ReleaseTwin.Hosted.Api
```

Real AWS (production) uses the standard AWS SDK credential chain instead — set `Aws__Region` (and optionally `Aws__DynamoDb__TablePrefix`) and omit `Aws__DynamoDb__ServiceUrl`; provision the table first via `cd hosted/terraform && terraform apply`.

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

## Support

Bugs and feature ideas → [GitHub issues](https://github.com/ernestoalejowitt22/ReleaseTwin/issues/new/choose).
Security → [private advisory](https://github.com/ernestoalejowitt22/ReleaseTwin/security/advisories/new).
Account, billing, or a pilot → email. Full routing in [`SUPPORT.md`](SUPPORT.md).

## What's not built yet

Deliberately deferred, not forgotten — each was a scoped decision, not an oversight:

- **Packaging/distribution** — a Docker image is now published (`cli-packaging`, see "Or run it via Docker" above), tag-triggered via a GitHub Actions release workflow. `dotnet tool`/NuGet and a GitHub Action wrapper are still deferred.
- **Azure DevOps operation parameters** — its operations are still fixed-shape; only the HTTP adapter is data-driven from case files.
- **A non-REST adapter** — the HTTP adapter covers anything with a REST surface; a message queue, database, or vendor SDK without one still needs bespoke adapter code.
- **Flag proof against a non-REST flag store** — a case drives the toggle through an adapter controller (Azure DevOps, LaunchDarkly) or a `flag_proof.control` HTTP request; a flag system with neither a controller adapter nor a REST toggle (an SDK-only or streaming provider) still needs a new `IFeatureStateController`. A project-level `control` template (rather than per-case) and a post-toggle read-back assertion are also still deferred.
- **External-check connector (Playwright)** — visual/browser evidence isn't wired in.
- **Billing** — the hosted platform (above) is Stage 1 only: no Stripe integration, no paid tiers, no usage enforcement.
- **Going public with the hosted platform** — it's deployed and a production Clerk instance is wired to it, but self-serve sign-up isn't linked or announced anywhere and no outside user has been invited. This is now a business decision, not a setup step.
- **Three-state prerequisites for other checks** — only Azure DevOps's `areaPathExists` check uses the inconclusive state; it's available to any adapter but nothing else has needed it yet.

## How far is this from commercial use?

Short answer: **technically promising, commercially unvalidated.** Two different bars, and only one has been cleared.

### Cleared: the technical go/no-go

The bar set at the start was specific — an adapter unrelated to the original use case must plug in "without modifying the core model or runner." That happened twice now: the Azure DevOps adapter, and then the generic HTTP adapter, both shipped without *unplanned* changes to `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` (two deliberate, tracked core evolutions did happen — three-state prerequisites, then operation parameters — each decided mid-implementation for a concrete reason, not forced blindly). That's real, repeated evidence the core/adapter boundary is sound, not just a story.

**The Tier 1/Tier 2 usage gap is now partially closed too** (see `docs/customer-pilot-guide.md`): a prospect with a REST API can author a real case testing real business behavior today, not just watch a fixed demo — proven end to end against a live public API, not a fake handler.

### Not yet started: the commercial go/no-go

The same assessment is explicit that this isn't enough on its own: *"Go if an unrelated second adapter can be implemented without ordinary changes to the core **and** design partners value the release-proof workflow."* The second half hasn't been touched:

- **No design partners contacted yet.** Nothing about willingness to pay, which reports matter, or whether "release-proof" as a concept resonates has been tested with an actual outside user.
- **No pricing validated.** The current pricing hypotheses are explicitly hypotheses, not offers anyone has seen.
- **No legal/entity work started.** Trademark search, incorporation, IP ownership documentation — none of it done. Tracked in the `company-and-domain-launch` change.
- **Not actually offered to anyone yet.** Self-serve signup/dashboard code exists and is tested (`hosted-self-serve-platform`, `clerk-registration`, `hosted-react-frontend`), but no Clerk application has been registered — a real one-time setup step, not a code gap. Even once that's done, running the CLI itself still means cloning source and having the .NET SDK — no packaging exists.

### Realistic framing

This is pre-pilot. If someone asked "can I buy this today," the honest answer is no — there's nothing to hand a paying customer, and the workflow's actual value hasn't been tested against a real business problem outside this project. The right next milestone is a paid design-partner engagement (one real workflow, a few weeks) — which is a sales/business-development step, not an engineering one. The engineering foundation to support that pilot conversation now exists; the conversation itself hasn't happened yet.
