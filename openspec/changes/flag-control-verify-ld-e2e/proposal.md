## Why

`flag-proof-control-readback` shipped the `flag_proof.control.verify` read-back
(new `ControlUnverified` outcome) with full unit coverage but one open task —
6.3: prove the generic HTTP flag-control + `verify` path works end-to-end against
a real feature-flag REST API, not just a stubbed `HttpMessageHandler`.

We already have the machinery: `web/cypress/e2e/launchdarkly-real-flag-proof.cy.ts`
runs the real CLI against a real LaunchDarkly flag, pulling credentials from AWS
Secrets Manager (`releasetwin/e2e/launchdarkly-account`) via the Cypress config's
`fetchLaunchDarklyTestAccount` task. That spec exercises the **LaunchDarkly
adapter** path (`ld.readFeatureFlag` + `LaunchDarklyFeatureStateController`). It
does **not** touch the vendor-neutral HTTP `control`/`verify` path, which is the
part with no real-endpoint coverage.

## What Changes

- **New Cypress e2e spec** (`launchdarkly-http-flag-control.cy.ts`): drives a
  `flag_proof` case whose `control` block toggles a real LaunchDarkly flag over
  LD's **REST API directly** (`PATCH /api/v2/flags/...`) and whose `control.verify`
  block reads that same flag back (`GET .../flags/...`, JSONPath on
  `environments.<env>.on`). No `ld.*` adapter operation — the always-present HTTP
  adapter is the only thing involved.
- **New Cypress config task** `writeHttpFlagControlCase` (sibling of
  `writeLaunchDarklyFlagProofCase`): generates the throwaway case + fixture
  directory, baking the caller-supplied flag key and the secret's environment key
  into the `control`/`verify` templates.
- **Happy path is the assertion**: real toggle + real read-back → deterministic
  `FLAGPROOF <id> (Passed)`, regardless of the flag's prior value (the case's own
  pipeline reads back the flag it just set, same self-check shape as the existing
  LD spec).
- **New `npm` script** `e2e:run:ld-http` and a short entry in
  `docs/flag-proof.md` / the demo README pointing at it.
- **Optional CI wiring** (see Design — may land here or as a follow-up): a
  `workflow_dispatch` + nightly GitHub Actions job that assumes an AWS role with
  `secretsmanager:GetSecretValue` on `releasetwin/e2e/launchdarkly-account` and
  runs the spec, matching the existing `deploy-hosted.yml` OIDC pattern.
- **Close task 6.3** on the archived `flag-proof-control-readback` change (tick it,
  note the covering spec).

## Capabilities

This change adds test coverage and tooling only — no product behavior changes, so
no spec deltas. `.openspec.yaml` sets `skip_specs: true`.

### New Capabilities

_None._

### Modified Capabilities

_None._

## Impact

- **`web/cypress/e2e/launchdarkly-http-flag-control.cy.ts`** — new spec.
- **`web/cypress.config.ts`** — new `writeHttpFlagControlCase` task; reuses the
  existing `fetchLaunchDarklyTestAccount`, `runCli`, and `ensureE2ETestUser` tasks
  unchanged.
- **`web/package.json`** — new `e2e:run:ld-http` script.
- **`.github/workflows/`** — optional new workflow (`ld-http-flag-control-e2e.yml`)
  gated on `workflow_dispatch`/`schedule`; needs the AWS OIDC role to allow
  `secretsmanager:GetSecretValue` on the LD e2e secret (credential-preflight item).
- **`docs/flag-proof.md`**, **`demo/README.md`** — one line each.
- **No changes** to `ReleaseTwin.Core`, `ReleaseTwin.Adapters.Http`, or the CLI —
  the code under test already shipped.

## Non-Goals

- A deterministic real-endpoint test of the **`ControlUnverified`** failure leg —
  making a real LD toggle silently no-op is not reliably reproducible; that path
  stays covered by the unit tests (`HttpFeatureStateControllerTests`,
  `CliRunnerFlagProofTests`).
- Replacing or merging with the existing adapter-path LD spec — both paths are
  worth covering independently.
- Adding LD REST toggling as a packaged adapter feature.
