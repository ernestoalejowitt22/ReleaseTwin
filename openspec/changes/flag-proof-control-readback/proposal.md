## Why

Flag proof's HTTP control request (shipped in `flag-proof-http-control`) sets the
target flag's state with one `POST`/`PATCH` and trusts a 2xx to mean the flag
actually changed. When the toggle endpoint returns 200 but the flag does not
flip — wrong key, wrong environment, eventual-consistency lag, a silently
ignored body, a flag that no longer exists — both legs then run against the
*same* real state and the run is reported as `WeakOracle` or `BothFailed`. That
points the finger at the customer's oracle when the real fault is their flag
plumbing. The product's headline claim is "we detect when your test can't tell
the fix from the bug"; it should cover "we detect when your flag toggle didn't
take" too, explicitly and by name.

## What Changes

- **New optional `control.verify` block** on a `flag_proof` case: one HTTP read
  request plus a JSONPath assertion. After the control request sets a leg's
  state and before that leg runs, the runner performs the read and confirms the
  flag reports the intended state.
- **New flag-proof outcome `ControlUnverified`** — the control request
  succeeded (2xx) but the read-back showed the flag did **not** reach the
  intended state. Reported as a failure, distinct from `ControlFailed` (the set
  request itself failed) and from `WeakOracle` / `BothFailed`, and it names
  which leg's state could not be confirmed.
- **Same substitution and credential contract as `control`**: `{{featureKey}}`,
  `{{state}}`, `{{enabled}}` tokens; `${ENV_VAR}` resolved from the environment
  or hosted `project-secrets` at load time; no literal credential in the case
  file. The verify request may reuse the control block's headers/auth by default.
- **Backward compatible**: a `flag_proof` case with no `verify` block behaves
  exactly as today — the read-back is opt-in.
- **Non-goal for this change**: project-level (`releasetwin.yaml`) control/verify
  templates, ret/poll-with-backoff on the read-back (single read only), and
  non-HTTP read-back.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `http-flag-control`: add a requirement that a `control` block MAY declare a
  `verify` read request + assertion, performed after each state change and
  before that leg; a failed read-back is surfaced to the runner as a distinct,
  non-2xx-independent condition.
- `flag-proof`: add the `ControlUnverified` outcome to the classification — a
  post-toggle read-back that does not show the intended state is a named
  failure, separate from `ControlFailed`, `WeakOracle`, and `BothFailed`, and
  neither leg is treated as having run under the intended state.

## Impact

- **`ReleaseTwin.Core`**: new `FlagProofOutcome.ControlUnverified`; a way for
  `IFeatureStateController.SetStateAsync` to signal "set accepted but not
  verified" as distinct from "set failed" (e.g. a dedicated exception type or a
  richer return), consumed by `FlagProofRunner`; message/`FlagProofResult`
  wiring.
- **`ReleaseTwin.Adapters.Http`**: `HttpFeatureStateController` performs the
  optional verify request after `SetStateAsync`, evaluates the JSONPath
  assertion, and raises the unverified signal on mismatch. Reuses the existing
  request builder, `${ENV}` interpolation, and JSONPath evaluator already used
  by `http.request` / the JSONPath assertion op.
- **`ReleaseTwin.Cli` case loading**: parse and validate `flag_proof.control.verify`
  (`method`, `url`, `headers`, `body`, `jsonpath`, `expected` — with
  `{{state}}`/`{{enabled}}` allowed in `expected`), same validation path as
  `control` and `http.request` parameters.
- **Reporting**: `CliRunner` console output and the machine-readable run summary
  gain the `ControlUnverified` verdict; the GitHub Action's rendered summary
  inherits it through the existing summary schema.
- **Docs / examples**: extend `example-flag-proof-http.yaml` with a `verify`
  block; note the new outcome in the flag-proof docs.
