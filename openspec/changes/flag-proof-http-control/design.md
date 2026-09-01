## Context

See `proposal.md` — Why. How flag proof works today:

- `ReleaseTwin.Core/FlagProof.cs`: `IFeatureStateController.SetStateAsync(featureKey, enabled, ct)`;
  `FlagProofRunner.RunAsync` gates on `ICapabilityCatalog.IsAvailable("flag-control:runtime")`,
  then calls `SetStateAsync(key, enabled: false)` → known-bad leg → `SetStateAsync(key, enabled: true)`
  → known-good leg, and folds the two into one `FlagProofOutcome`.
- `IFeatureStateController` implementations: `VariableGroupFeatureStateController`
  (writes `"true"`/`"false"` to an AzDO variable-group variable) and
  `LaunchDarklyFeatureStateController`. Both adapters `.AddCapability("flag-control:runtime")`.
- `src/ReleaseTwin.Cli/CliRunner.cs`: resolves one controller per run from
  `[azureDevOpsAdapter, launchDarklyAdapter]` via `IFeatureStateControllerSource`;
  per flag-proof case, `if (controller is null)` → prints `Ineligible`, else
  builds a `FlagProofRunner` and runs.
- `CaseFileLoader.ResolveFlagProof` parses `flag_proof: { feature_key, build_identity }`
  into a `FlagProofDeclaration`; `InterpolateEnvVars` already resolves `${VAR}`
  (env → hosted project secrets) for `http.request` parameters at load time.

## Goals / Non-Goals

**Goals:**
- Flag proof runs against any flag system with a REST toggle, from case data alone.
- A rejected toggle is a distinct, visible *failure* — never a silent pass or a
  misleading "ineligible".
- No hosted change; flag control stays CLI-side in the customer's infra.

**Non-Goals:**
- Project-level (`releasetwin.yaml`) control templates — per-case only for now.
- A response assertion on the control request (read the flag back).
- Non-HTTP toggles (a shell command, a file write).
- Evidence from the control request itself — the legs' own pipeline evidence is
  the artifact.

## Decisions

### D1: Control lives in the case's `flag_proof.control` block

```yaml
flag_proof:
  feature_key: checkout-v2
  build_identity: orders@2f9c1a
  control:
    method: PUT
    url: ${FLAGS_API}/flags/{{featureKey}}
    headers: { Authorization: "Bearer ${FLAGS_TOKEN}" }
    body: '{ "state": "{{state}}" }'
    known_bad_when: disabled   # optional; default
```

- **Why per-case:** flag-proof cases are rare (it's the special mechanic), YAML
  anchors handle the "same flag service, many cases" case, and a `releasetwin.yaml`
  schema for structured adapter config doesn't exist yet (it only holds an
  `adapters:` name list). Add project-level config if a design partner's volume
  warrants it — deferred, not foreclosed.

### D2: Polarity lives in the HTTP controller, not Core

Core keeps calling `SetStateAsync(key, enabled: false)` for known-bad and
`enabled: true` for known-good — unchanged. `HttpFeatureStateController` holds
`known_bad_when` and computes the customer-facing flag state:

```
flagOn = knownBadWhen == "disabled" ? coreEnabled : !coreEnabled
{{state}}   -> flagOn ? "enabled" : "disabled"
{{enabled}} -> flagOn ? "true"    : "false"
```

- **Why:** the only place polarity matters is the request template; keeping it out
  of Core means `FlagProofRunner`'s leg logic and every other controller stay
  untouched.

### D3: The HTTP adapter registers `flag-control:runtime` unconditionally

`HttpAdapter` gains `.AddCapability("flag-control:runtime")`, matching how the
AzDO / LD adapters register it whether or not their specific backing is
configured. The **real** per-case gate stays in the CLI: build the effective
controller as `flagProof.Control is { } c ? new HttpFeatureStateController(c, …) : adapterController`,
and `if (effectiveController is null)` → ineligible.

- **Why:** the capability catalog is built once at composition; the `control`
  block is per-case. Rather than thread per-case capability into the catalog, the
  always-installed HTTP adapter advertises the capability and the CLI decides
  eligibility per case — which it already does for the null-controller path.
- **Alternative rejected:** pass a custom `requiredCapability` string to
  `RunAsync` for the HTTP path. Opaque; the catalog check becomes meaningless.

### D4: `HttpFeatureStateController` in `ReleaseTwin.Adapters.Http`

A small type: holds the parsed `control` (method / url / headers / body, all with
`${VAR}` already resolved at load), plus `known_bad_when`, plus an `HttpClient`.
`SetStateAsync` builds an `HttpRequestMessage` with the `{{…}}` tokens substituted
for this leg, sends it, and **throws `FlagControlException` on a non-2xx or a
transport failure**. Reuses the HTTP adapter's existing client and the same
"no captures, no evidence buffer" execution shape as a bare `http.request`.

### D5: `FlagProofOutcome.ControlFailed` + a catch in `RunAsync`

`RunAsync` wraps each `SetStateAsync` call:

```
try { await _featureStateController.SetStateAsync(key, enabled: false, ct); }
catch (Exception ex) { return FlagProof result with Outcome = ControlFailed, both legs null; }
```

`ControlFailed` sorts as *failing* everywhere (`result.Outcome == Passed` is the
only success check). The CLI prints `FLAGPROOF <id> (ControlFailed): <message>`
and the hosted `Outcome` string carries it through to the dashboard.

- **Why a new enum value:** the spec requires this be distinct from weak-oracle
  and from ineligible. `Ineligible` means "we didn't try"; `ControlFailed` means
  "we tried to set the flag and the flag service said no" — a real, actionable
  signal about the customer's flag plumbing.

### D6: `${VAR}` at load, `{{token}}` at execution

`${FLAGS_TOKEN}` etc. resolve in `CaseFileLoader` via the existing
`InterpolateEnvVars` (env → hosted project secrets), so a missing secret is a
load error and the parsed `control` holds no reference. `{{featureKey}}` /
`{{state}}` / `{{enabled}}` are substituted by the controller per leg, since
`{{state}}` differs between legs.

## Risks / Trade-offs

- **The control request "succeeds" (2xx) but the flag didn't actually flip** → the
  known-bad / known-good legs functionally prove the state took effect: if the
  toggle silently no-oped, both legs see the same state → `WeakOracle` or
  `BothFailed`, which is already a visible failure. The deferred read-back
  assertion would make it explicit.
- **A slow flag service between legs** → the control request uses the HTTP
  adapter's client timeout; a hang surfaces as `ControlFailed`, not a wedged run.
- **Secret leakage in evidence** → the control request produces no evidence, and
  its `${VAR}` values are already in the CLI redactor's mask list (project
  secrets + known credential env vars).

## Migration Plan

Additive. A case with no `control` block behaves exactly as today. Rollout: land
the parser + controller + Core enum together; ship an
`examples/cases/example-flag-proof-http.yaml` and a docs section. No hosted
deploy, no infra.

**Rollback:** the `control` block is optional and the new enum value only appears
when a control request fails; removing the feature reverts to "AzDO / LD only".

## Open Questions

- Token syntax: `{{featureKey}}` (chosen, distinct from `${ENV}`) vs `${featureKey}`
  (collides with env interpolation). Settled on `{{…}}`.
- Whether `{{state}}` should also offer the raw boolean form only (`{{enabled}}`)
  or both — keeping both; costs nothing.
