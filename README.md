# ReleaseTwin

Release-proof testing for integration-heavy, feature-flagged systems:
evidence-linked cases, immutable fixtures, prerequisite ownership, deterministic
evidence, and paired known-bad/known-good **flag proof** — the mechanic that
distinguishes a broken build from a fixed one, not just "green in one
environment."

The engine runs entirely in your own infrastructure — your laptop or your CI
runner. It needs no account and makes no network call to any ReleaseTwin service
to run a case. A hosted dashboard for run history and evidence is available at
[releasetwin.com](https://releasetwin.com); it is optional and this repo does not
depend on it.

**New here?** [docs/quickstart.md](docs/quickstart.md) — test your first API in
10 minutes with `docker run`, no account, no clone.

## Licence

The engine — `src/`, `tests/`, `docs/`, `openspec/` — is **AGPL-3.0-only WITH the
ReleaseTwin Adapter Linking Exception** ([`LICENSE`](LICENSE),
[`LICENSE.EXCEPTIONS`](LICENSE.EXCEPTIONS)). The exception means an **independent
adapter** — one that plugs into the published `AdapterSdk` / `Core` extension
points and is not a derivative of the engine's internals — **may be licensed
however you like**, including proprietary. You can write and ship a closed-source
adapter for your own systems without AGPL obligations on that adapter.

`examples/` and `integrations/` are **Apache-2.0**. See [`LICENSING.md`](LICENSING.md)
for the full map and the reasoning.

## What's here

| Project | What |
|---|---|
| **`ReleaseTwin.Core`** | The execution kernel: ordered pipelines, fixture integrity, three-state prerequisites, cleanup, retry/timeout, resource-key serialization, failure classification, flag proof. |
| **`ReleaseTwin.AdapterSdk`** | The composition-root pattern that lets adapters plug in without touching the core. |
| **`ReleaseTwin.Adapters.Http`** | Vendor-neutral, parameterized: `http.request` + `http.assertJsonPath` test *any* REST API from case-file data alone, no adapter code per target. No credentials to install. |
| **`ReleaseTwin.Adapters.AzureDevOps`** | One fixed-shape real adapter — work items, prerequisites, cleanup, variable-group flag proof. |
| **`ReleaseTwin.Adapters.LaunchDarkly`** | Feature-state control against LaunchDarkly for flag proof. |
| **`ReleaseTwin.Adapters.Ui`** | Opt-in browser leg (Playwright/Chromium): `ui.navigate` / `click` / `fill` / `waitFor` (selector **or** SPA URL) / `assertVisible` / `assertText` / `setCookie`, chained to API legs by the same value-capture. Drives React/Angular/any app — see [docs/spa-testing.md](docs/spa-testing.md). |
| **`ReleaseTwin.Cli`** | Loads YAML case files, composes adapters (from `releasetwin.yml` or auto-detected credentials), reports pass/fail with a CI-usable exit code. Optionally uploads run metadata to the hosted dashboard when an API token is set. |
| `ToyHttp` / `ToyFile` | Exist only to stress-test the adapter boundary. |
| **`integrations/github-action/`** | Apache-2.0 GitHub Action — runs the CLI and renders the result onto a pull request as a comment + check run, using only the workflow's `GITHUB_TOKEN`. |

## Running the example

### Prerequisites

- .NET 8 SDK (or Docker — see below).
- Nothing else for the HTTP example — it needs no credentials.
- For the Azure DevOps example: an org, project, a PAT with Work Item read/write, and a variable group.

### Build and run

```bash
git clone https://github.com/ernestoalejowitt22/ReleaseTwin.git
cd ReleaseTwin
dotnet build ReleaseTwin.sln
dotnet run --project src/ReleaseTwin.Cli -- examples/cases
```

`example-http.yaml` is a real HTTP call to a public test API with two real
JSONPath assertions — against the live internet, no fake handler, no credentials.
`example-claim.yaml` and `example-flag-proof.yaml` need Azure DevOps; without its
5 environment variables they report as failing (`missing-capability` /
`Ineligible`) rather than crashing. A non-zero exit code means at least one case
failed — wire it straight into a CI step.

### Via Docker (no .NET SDK)

```bash
docker pull ghcr.io/ernestoalejowitt22/releasetwin/cli:0.1.0   # pin a version — avoid :latest in CI
docker run --rm -v "$(pwd)/examples:/workspace:ro" ghcr.io/ernestoalejowitt22/releasetwin/cli:0.1.0
```

The container expects a `cases/` directory with a sibling `fixtures/` directory
under whatever you mount to `/workspace`. `--env-file .env` or bare `-e SOME_VAR`
pass credentials through. Exit codes propagate. See
[docs/install.md](docs/install.md) for the `dotnet tool` and GitHub Action paths.

## Writing a case

**Any REST API** (no adapter code — see `examples/cases/example-http.yaml`):

```yaml
id: MY-CASE-1
release: "4.2"                    # optional grouping label
oracle:
  locator: tickets/MY-CASE-1
fixture:
  locator: my-fixture.json        # resolved relative to a fixtures/ dir next to cases/
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

**Against Azure DevOps** (fixed-shape, see `examples/cases/example-claim.yaml`):

```yaml
id: MY-CASE-2
oracle: { locator: tickets/MY-CASE-2 }
fixture: { locator: my-fixture.json, sha256: <sha256> }
requires: [http:azure-devops]
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

Operation names today: **Generic HTTP** — `http.request`, `http.assertJsonPath`.
**Azure DevOps** — `azdo.createWorkItem`, `azdo.getWorkItem`,
`azdo.transitionWorkItemState`, `azdo.areaPathExists`, `azdo.deleteWorkItem`,
`azdo.readFeatureVariable`. (Azure DevOps operations still take no per-case
parameters; the HTTP adapter is fully data-driven.)

## Flag proof

Add a `flag_proof` block and the CLI runs the case twice — flag off, then on —
reporting one discriminating outcome instead of a plain pass/fail:

```yaml
flag_proof:
  feature_key: checkout-v2
  build_identity: orders@2f9c1a
  control:                                        # when no adapter drives the flag: toggle it over REST
    method: PUT
    url: ${FLAGS_API}/flags/{{featureKey}}        # {{featureKey}} / {{state}} / {{enabled}} substituted per leg
    headers: { Authorization: "Bearer ${FLAGS_TOKEN}" }
    body: '{ "state": "{{state}}" }'
    known_bad_when: disabled                      # or `enabled` to invert
```

The toggle is driven by an installed adapter that exposes feature-state control
(Azure DevOps variable group, LaunchDarkly) or the `control` block above.
Outcomes: `Passed`, `WeakOracle` / `BothFailed` / `Inverted` (the pipeline can't
tell the legs apart), `Ineligible` (nothing can drive the toggle), `ControlFailed`.
For a suite of flag-proof cases against one system, declare the `control` block
once in a `releasetwin.yml` at the cases-directory root — see
[docs/flag-proof.md](docs/flag-proof.md).

## Tests

```bash
dotnet test ReleaseTwin.sln
```

All offline (fake HTTP responses) except two Azure DevOps integration tests that
skip unless `AZDO_*` point at a real sandbox. CI (`.github/workflows/ci.yml`)
runs the full suite on every push and PR; `release.yml` tests, builds, and pushes
the multi-arch CLI image on `v*.*.*` tags.

## Support

Bugs and feature ideas → [GitHub issues](https://github.com/ernestoalejowitt22/ReleaseTwin/issues/new/choose).
Security → [private advisory](https://github.com/ernestoalejowitt22/ReleaseTwin/security/advisories/new).
Anything else → see [`SUPPORT.md`](SUPPORT.md).

## What's not built yet

Deliberately deferred, each a scoped decision:

- **Packaging** — Docker image, `dotnet tool` (`dotnet tool install -g releasetwin`), and the GitHub Action all ship from the release workflow ([docs/install.md](docs/install.md)). A Homebrew tap and a single-file binary are deferred.
- **Azure DevOps operation parameters** — still fixed-shape; only the HTTP adapter is data-driven.
- **A non-REST adapter** — anything without a REST surface (a message queue, a vendor SDK) still needs bespoke adapter code.
- **Flag proof against an SDK-only / streaming flag store** — needs a new `IFeatureStateController`.
- **External-check connector** — folding an *externally run* Playwright/Cypress suite's results into a case. (The built-in `ui.*` adapter already drives a browser as a pipeline leg and captures per-step screenshots + a session `.webm` under `RELEASETWIN_EVIDENCE=on` — see [docs/spa-testing.md](docs/spa-testing.md).)
- **Three-state prerequisites elsewhere** — only Azure DevOps's `areaPathExists` uses the inconclusive state; it's available to any adapter.
