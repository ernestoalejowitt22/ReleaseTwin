# Flag proof

Flag proof runs one case **twice against the same fixture and build** — once with the
target feature known-bad (off), once known-good (on) — and reports a single
discriminating verdict instead of a plain pass/fail:

| Outcome | Meaning |
|---|---|
| `Passed` | known-bad failed, known-good passed — the case's oracle genuinely discriminates |
| `WeakOracle` | both legs passed — the oracle can't tell the fix from the break |
| `BothFailed` | neither leg passed |
| `Inverted` | known-bad passed, known-good failed — the oracle points the wrong way |
| `Ineligible` | nothing could drive the toggle (no adapter controller, no `control` block) |
| `ControlFailed` | the toggle request itself errored; the run could not be performed |
| `ControlUnverified` | the toggle was accepted (2xx) but a read-back showed the flag never changed |

## Declaring it

Add a `flag_proof` block to any case:

```yaml
flag_proof:
  feature_key: release-proof-feature   # the flag to toggle
  build_identity: build-123            # carried through the report unchanged
```

The toggle is driven by whichever installed adapter exposes feature-state control:

- **Azure DevOps** — writes `true`/`false` to a variable-group variable.
- **LaunchDarkly** — flips the flag's targeting via the LaunchDarkly REST API.

## Toggling a flag no adapter knows about (`flag_proof.control`)

When the flag lives in a system with no adapter — Flagsmith, Unleash, a config
service, your own endpoint — add a `control` block. The always-present HTTP adapter
sends one request per leg to set the state:

```yaml
flag_proof:
  feature_key: checkout-v2
  build_identity: orders@2f9c1a
  control:
    method: PUT                                   # GET | PUT | POST | PATCH | DELETE
    url: ${FLAGS_API}/flags/{{featureKey}}
    headers:
      Authorization: "Bearer ${FLAGS_TOKEN}"
    body: '{ "state": "{{state}}" }'
    known_bad_when: disabled                      # default; use `enabled` to invert
```

### Substitution

- `${ENV_VAR}` is resolved once at **case-load time** — from your environment, falling
  back to this project's hosted secrets. The case file **must never contain a literal
  credential**; a bare token is not accepted, only `${VAR}`.
- `{{featureKey}}`, `{{state}}` (`enabled`/`disabled`), `{{enabled}}` (`true`/`false`)
  are substituted **per leg**, in the URL, header values, and body.

### Polarity

`known_bad_when: disabled` (the default) means the buggy behaviour appears when the
flag is **off** — so the known-bad leg drives it off and the known-good leg drives it
on. Set `known_bad_when: enabled` when the flag *is* the bug (a half-built feature
behind it) and the safe state is off.

### Reading the flag back (`control.verify`)

A toggle endpoint that returns `200` but silently ignores the request — wrong key,
wrong environment, a body it doesn't understand, a deleted flag — leaves both legs
running against the *same* real state, which surfaces as a misleading `WeakOracle`
or `BothFailed`. Add an optional `verify` block to catch it:

```yaml
    verify:
      method: GET                                 # optional, default GET
      url: ${FLAGS_API}/flags/{{featureKey}}
      # headers:                                  # optional; defaults to control.headers
      json_path: $.state
      expected: "{{state}}"                        # {{state}} / {{enabled}} allowed here
```

After each toggle and before that leg runs, the HTTP adapter issues the `verify`
request and checks that `json_path` in the response equals `expected` (a JSON
boolean matches `"true"` / `"false"`). If it doesn't, the run ends as
`ControlUnverified` — naming the leg whose state couldn't be confirmed — instead of
running a leg under the wrong flag state. When `headers` is omitted the `control`
block's headers (and their `${VAR}` auth) are reused.

Only enable `verify` when your flag service is **read-your-writes consistent**; a
service that takes a moment to propagate a change can report a false
`ControlUnverified`. A single read is performed — there is no retry or poll.

### Failures

A non-2xx response — or a request that can't be sent — ends the run as
`ControlFailed`, naming the method, URL, and status (this covers a `verify` request
that itself errors). No leg executes after a failed control call, so a broken toggle
can never be misreported as a weak oracle.

See `examples/cases-flag-proof-http/example-flag-proof-http.yaml` for a complete case.
