# Engineering Overview

How the CLI, execution kernel, and adapters fit together — grounded in the actual code, pointing at [`openspec/specs/`](../../openspec/specs/) for the authoritative behavior contract on anything this doc only summarizes. Treat the specs as ground truth; this is connective narrative, not a replacement.

This repo (`ReleaseTwin`) is the AGPL-3.0 engine — CLI, execution kernel, and adapters — with an Adapter Linking Exception that lets independently-written adapters plugging into `AdapterSdk`/`Core` be licensed however their author chooses, proprietary included. The hosted SaaS control plane (dashboard, billing, ticket-tracker credentials) lives in a separate, private, BSL-licensed repo and is out of scope here.

## CLI entry points

| Command | Purpose |
|---|---|
| `releasetwin init [--from-examples]` | Scaffold a new project (case + fixture + config) in the cwd. Never destructive. |
| `releasetwin new <case-id>` | Add one more case + fixture to an existing project. |
| `releasetwin run [dir]` | Execute cases in `dir` (default `./cases`). Bare `releasetwin <dir>` also works. |
| `releasetwin run --journey <id>@<version>` | Fetch a pinned journey from the hosted service and execute it — the only flow that talks to the network by default. |
| `--summary-json <path>` / `--junit-xml <path>` | CI-consumable output, or via `RELEASETWIN_SUMMARY_JSON` / `RELEASETWIN_JUNIT_XML`. |

No network call or account is required for `run` against local cases; hosted upload/journey-fetch is opt-in via an API token. Spec: [`case-scaffolding`](../../openspec/specs/case-scaffolding/spec.md), [`case-loading`](../../openspec/specs/case-loading/spec.md).

## Execution kernel — `CaseExecutor.ExecuteAsync`

One ordered pipeline per case. Every concern below is vendor-neutral; the core has zero references to any specific adapter or product — full contract in [`core-execution`](../../openspec/specs/core-execution/spec.md).

1. **Acquire lock** — optional `resource_key` serializes cases sharing a resource.
2. **Verify capabilities** — all `RequiredCapabilities` installed, else fail `missing-capability`.
3. **Validate references** — operation/prerequisite/cleanup names resolve against the composed catalogs.
4. **Verify fixture integrity** — sha256 match, else fail `fixture-integrity-mismatch`.
5. **Evaluate prerequisites** — three states: Satisfied / NotSatisfied (→ Prerequisite failure) / Inconclusive (→ Infrastructure failure). Cleanup still runs on a halted case.
6. **Run the pipeline** — resolve `{{capture}}` references, execute each step with retry/timeout, record evidence, classify failures as Product / Infrastructure / Unstable.
7. **Always run cleanup** — even on early exit.
8. **Emit CaseReport** — plus optional structured `RunEvidence` when capture is on ([`evidence-capture`](../../openspec/specs/evidence-capture/spec.md)), traceable to the case id and oracle locator.

## Flag proof

Runs a case's pipeline **twice** against the same immutable build/fixture — once forced known-bad, once known-good — via an installed `IFeatureStateController` (Azure DevOps variable group, LaunchDarkly) or an inline `control:` HTTP block ([`http-flag-control`](../../openspec/specs/http-flag-control/spec.md)). Outcome is a direct tuple match on the two legs' pass/fail:

| | known-good passed | known-good failed |
|---|---|---|
| **known-bad failed** | **Passed** | BothFailed |
| **known-bad passed** | WeakOracle | Inverted |

`Ineligible` (no control mechanism available) and `ControlFailed`/`ControlUnverified` (the toggle itself broke) short-circuit before either leg's result matters. Full contract: [`flag-proof`](../../openspec/specs/flag-proof/spec.md).

## Adapters

Composition-root pattern — adapters register operations, prerequisite checks, cleanup handlers, and capability declarations into the core; the core stays adapter-agnostic. Contract: [`adapter-sdk`](../../openspec/specs/adapter-sdk/spec.md).

| Adapter | Role | Spec |
|---|---|---|
| `Adapters.Http` | Vendor-neutral REST driver (`http.request`, `http.assertJsonPath`, OAuth2 client-credentials). Also implements `HttpFeatureStateController` — any HTTP-reachable flag store can drive flag proof with zero adapter code. | [`http-adapter`](../../openspec/specs/http-adapter/spec.md) |
| `Adapters.AzureDevOps` | The one fixed-shape "real" adapter: work-item CRUD/transition, an `areaPathExists` prerequisite check, and a variable-group flag controller. | — |
| `Adapters.LaunchDarkly` | Toggles a real LD flag around a paired known-bad/known-good run. | [`launchdarkly-adapter`](../../openspec/specs/launchdarkly-adapter/spec.md) |
| `Adapters.Ui` | Opt-in Playwright/Chromium leg (navigate/click/fill/waitFor/assert…), chained to API legs via the same capture mechanism. Per-step screenshot + session `.webm` evidence under `RELEASETWIN_EVIDENCE=on`. | [`ui-adapter`](../../openspec/specs/ui-adapter/spec.md) |
| `ToyHttp` / `ToyFile` | Test-only — stress the adapter boundary, not shipped functionality. | — |

Not yet built: non-REST adapters (message queue, vendor SDK), SDK-only/streaming flag stores, an external-check connector for externally-run Playwright/Cypress suites, three-state prerequisites outside Azure DevOps.

## Feature flags — a second, unrelated meaning of "flags"

Separate from flag proof's known-bad/known-good mechanic: a vendor-neutral flag-evaluation registry shared across this CLI, the web dashboard, and the hosted API. Contract: [`feature-flags`](../../openspec/specs/feature-flags/spec.md). Don't conflate the two — flag proof is about proving a *customer's* flag gates real behavior; this is about ReleaseTwin's own runtime configuration.

## CI integration & distribution

- **Credential resolution** ([`cli-runner`](../../openspec/specs/cli-runner/spec.md)): adapter credentials resolve from environment first, falling back to a hosted `adapter-credentials` fetch only when a project API token is set and env vars are entirely unset — partial env config always errors, never silently falls back.
- **CI report formats** ([`ci-report-formats`](../../openspec/specs/ci-report-formats/spec.md)): JUnit XML output for CI test widgets.
- **CI/PR integration** ([`ci-pr-integration`](../../openspec/specs/ci-pr-integration/spec.md)): the OSS GitHub Action, Bitbucket Pipe, and GitLab Component (in `integrations/`, Apache-2.0) render a run summary onto a PR (comment + check run) using only the workflow's own token — no hosted service required.
- **Packaging** ([`cli-packaging`](../../openspec/specs/cli-packaging/spec.md)): Docker image, `dotnet tool`, GitHub Action release artifacts.
- **Enterprise access** ([`enterprise-access`](../../openspec/specs/enterprise-access/spec.md)): running against network-isolated (VPN/VPC) and identity-gated (Entra ID/org OAuth) targets.

## Where the hosted platform picks up

The CLI's `--journey` fetch and evidence/credential upload are this repo's only points of contact with the hosted SaaS control plane — everything else (`init`/`new`/`run` against local cases) works standalone, no account required. What happens on the hosted side after that point (ingest, storage, ticket write-back, billing) is documented in the `releasetwin-platform` repo's own `docs/engineering/`, not here.
