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

### Flag APIs behind Entra ID / org OAuth (`control.auth`)

When the toggle endpoint is gated by Microsoft Entra ID (or any OAuth2
client-credentials flow), add an `auth` block. Before the control request for
**each leg**, the HTTP adapter performs a client-credentials exchange against the
token endpoint and substitutes the resulting token for `{{token}}`:

```yaml
    auth:
      oauth2_client_credentials:
        token_url: https://login.microsoftonline.com/${AZURE_TENANT_ID}/oauth2/v2.0/token
        client_id: ${FLAGS_CLIENT_ID}
        client_secret: ${FLAGS_CLIENT_SECRET}
        scope: ${FLAGS_SCOPE}          # optional; e.g. api://<app>/.default
    headers:
      Authorization: "Bearer {{token}}"
```

`${VAR}` resolves at case-load time as everywhere else — the client secret is
never in the file. A failed token exchange (non-2xx, unreachable, or a response
with no `access_token`) ends the run as `ControlFailed`, the same as a rejected
toggle; the leg does not run. See
[`docs/enterprise-access.md`](enterprise-access.md) for the full enterprise
(VPN + org SSO) picture and
`examples/cases/enterprise/example-flag-proof-http-entra.yaml`.

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

The `control` + `verify` path is exercised end-to-end against a real
feature-flag REST API (LaunchDarkly's) by
`web/cypress/e2e/launchdarkly-http-flag-control.cy.ts`, run on demand and
nightly by `.github/workflows/ld-http-flag-control-e2e.yml` — both in the
private `releasetwin-platform` repo, not here.

### Shared control template (`releasetwin.yml`)

When a suite has several flag-proof cases against the *same* flag system, the
`control` block is the same in every file except `feature_key`. Declare it once in
a `releasetwin.yml` at the root of your cases directory:

```yaml
# cases/releasetwin.yml
flag_proof:
  control:
    method: PUT
    url: ${FLAGS_API}/flags/{{featureKey}}
    headers:
      Authorization: "Bearer ${FLAGS_TOKEN}"
    body: '{ "state": "{{state}}" }'
```

```yaml
# cases/checkout-flag.yaml
flag_proof:
  feature_key: checkout-v2        # the only per-case difference
  build_identity: orders@2f9c1a
```

- A `flag_proof` case with **no** `control` block inherits the manifest's whole.
- A case that declares a `control` block **merges** it over the manifest: scalar
  fields and individual `headers` entries override; an `auth` or `verify` block on
  the case replaces the manifest's entirely.
- `${VAR}` and `{{...}}` resolve exactly as in an inline block. The manifest must
  never hold a literal credential.
- Adding a `releasetwin.yml` only supplies fields a case omits — a case with a
  complete inline `control` is unaffected. A malformed manifest, an unknown key,
  or a merged block still missing `url` is a load error before any case runs.
- The file is `releasetwin.yml` (a `.yaml` spelling also works); it is never
  loaded as a case itself.

### Failures

A non-2xx response — or a request that can't be sent — ends the run as
`ControlFailed`, naming the method, URL, and status (this covers a `verify` request
that itself errors). No leg executes after a failed control call, so a broken toggle
can never be misreported as a weak oracle.

See `examples/cases-flag-proof-http/example-flag-proof-http.yaml` for a complete
case, and `examples/cases-flag-proof-shared-control/` for a two-case suite that
shares one `releasetwin.yml`.
