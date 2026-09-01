## Context

See proposal.md — Why. Relevant current state:

- `FlagProofRunner.RunAsync` (`src/ReleaseTwin.Core/FlagProof.cs`) drives the pair
  by calling `IFeatureStateController.SetStateAsync(key, enabled, ct)` before each
  leg. It catches any non-cancellation exception from that call and maps it to a
  single `FlagProofOutcome.ControlFailed`. Leg pass/fail then maps to
  `Passed` / `WeakOracle` / `BothFailed` / `Inverted`.
- The HTTP path's controller, `HttpFeatureStateController`, is built per-case by
  the CLI from the resolved `flag_proof.control` block and throws
  `FlagControlException` on a non-2xx or unsendable request. Polarity
  (`known_bad_when`) is resolved inside the controller (prior change's D2).
- `JsonPathAssertOperation` already does exactly the read-body → `JToken.Parse` →
  `SelectToken(path)` → ordinal string compare that a read-back needs, including
  the `actual?.ToString()` scalar-stringification that makes `expected: "true"`
  match a JSON boolean.
- The only consumers of `FlagProofOutcome` are `FlagProof.cs` itself and
  `CliRunner.cs` (console line + `flagProofOutcome` string in the machine
  summary). No hosted code branches on it.

## Goals / Non-Goals

**Goals:**
- A read-back that contradicts the intended state produces one specific,
  named verdict (`ControlUnverified`) — never a leg-level misreport.
- Zero change to `FlagProofRunner`'s leg logic and zero change to the
  non-HTTP controllers (`AzureDevOps`, `LaunchDarkly`).
- Read-back assertion semantics identical to the existing `http.assertJsonPath`
  operation, so authors learn one comparison model.

**Non-Goals (design-level):**
- Poll/retry/backoff on the read-back — one read only. A `verify.retry` block is
  a future additive change that would not touch the outcome set.
- A read-back for the non-HTTP controllers — they own their own verification if
  they ever need it.
- Reading the flag back through a non-HTTP channel.

## Decisions

### D1: A new exception type, not a richer `SetStateAsync` return

`HttpFeatureStateController` throws a new
`FlagStateUnverifiedException` (sibling of `FlagControlException`) when the set
request succeeds (2xx) but the `verify` assertion fails.
`FlagProofRunner` adds one `catch (FlagStateUnverifiedException)` ahead of its
existing catch, mapping to `FlagProofOutcome.ControlUnverified` with a message
naming the leg.

- **Why not change `IFeatureStateController` to return a status object?** Three
  controllers implement it; a signature change touches all of them and the core
  contract for a concern only one cares about. The exception seam is already how
  "control problem" reaches the runner.
- **Why a distinct type rather than reusing `FlagControlException`?** The runner
  must tell "toggle endpoint broke" (`ControlFailed`) from "toggle silently did
  nothing" (`ControlUnverified`) — different diagnoses for the customer.
- A `verify` read request that itself returns non-2xx / cannot be sent throws
  the existing `FlagControlException` → `ControlFailed`. Only an assertion
  *mismatch* is `ControlUnverified`.

### D2: The read-back runs inside the controller, after the set

`SetStateAsync` performs: set request → (if `verify` present) read request →
assert. This keeps polarity resolution, substitution, and now verification all
in one place, and the runner stays oblivious to HTTP.

- **Why not have the runner orchestrate the read-back?** The runner would need
  the verify config and an HTTP client — re-opening the seam the prior change
  closed.

### D3: Reuse the JSONPath assertion logic verbatim

Extract the `JToken.Parse(body).SelectToken(path)` + `actual?.ToString()` +
`string.Equals(..., Ordinal)` core of `JsonPathAssertOperation` into a small
internal shared helper in `ReleaseTwin.Adapters.Http`, and call it from both the
operation and the controller. Substitution of `{{state}}` / `{{enabled}}` /
`{{featureKey}}` into `expected` and `url` happens with the controller's
existing `Substitute` before the helper runs.

- **Why share rather than re-implement?** Divergent JSONPath/comparison behavior
  between the assertion op and the read-back would be a latent author-confusion
  bug.
- **Implementation note:** the shared helper also normalizes a JSON boolean token
  to `"true"` / `"false"` (Newtonsoft stringifies it as `"True"` / `"False"`), so
  `expected: "{{enabled}}"` matches naturally. This slightly changes
  `http.assertJsonPath` for boolean targets — a fix, and no existing test or
  example relied on the `"True"` form.

### D4: `verify` config shape and CLI validation

```
flag_proof:
  control:
    method: PATCH
    url: https://flags.example.com/api/flags/{{featureKey}}
    headers: { Authorization: "Bearer ${FLAGS_TOKEN}" }
    body: '{"enabled": {{enabled}}}'
    known_bad_when: disabled
    verify:
      method: GET                     # optional, default GET
      url: https://flags.example.com/api/flags/{{featureKey}}
      headers: {}                     # optional, defaults to control.headers
      jsonpath: "$.enabled"
      expected: "{{enabled}}"
```

The CLI's case loader validates `verify` on the same path as `control` /
`http.request`: `url` required and non-empty, `method` a known verb, `jsonpath`
and `expected` required non-empty strings, `${ENV_VAR}` resolved at load time,
no literal credential. A `verify` block on a case with no `control` block is a
load error.

## Risks / Trade-offs

- **A genuinely eventually-consistent flag service → false `ControlUnverified`.**
  → Opt-in; documented as "only enable if your toggle is read-your-writes
  consistent"; `verify.retry` is the clean future escape hatch and needs no
  outcome-set change.
- **Secret leakage in the failure message.** → The `FlagStateUnverifiedException`
  message carries only method, URL, `jsonpath`, `expected`, and the scalar
  `actual` — never the full response body. `${VAR}`-resolved values are already
  in the CLI redactor's mask list.
- **New enum value breaks an exhaustive `switch` somewhere.** → Only `CliRunner`
  consumes it and it uses `.ToString()` / equality, not an exhaustive switch;
  the runner's own leg-outcome switch is unaffected (it's reached only after a
  verified set). Add the console/summary branch in the same change.
- **Verify request doubles the per-leg HTTP calls to the flag service.** →
  Acceptable: two extra GETs per flag-proof run, opt-in.

## Migration Plan

Additive. Deploy with the CLI release. No config migration: existing
`flag_proof.control` blocks without `verify` are byte-for-byte unchanged in
behavior. Rollback is removing the `verify` block (or an older CLI, which
ignores it — though validation would then not reject a malformed one).
