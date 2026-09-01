## Context

See `proposal.md` — Why. This design captures four threads from an explore
session on the objection "everything is behind our VPN and organization
Microsoft (Entra ID) OAuth."

Current state that shapes the approach:

- **Execution locus.** The engine is the AGPL CLI, run in the customer's CI on
  their runners (`cli-runner`, `cli-packaging`). The hosted platform is
  ingest-only: the CLI pushes evidence and verdicts up via `ingest-api`; nothing
  in the hosted platform connects down into a customer network.
- **HTTP auth already exists.** `http-adapter` has
  `An OAuth2 client-credentials exchange is a single convenience step` and
  captured-value reuse (`A captured token is used as a bearer header`). This is
  sufficient for the Entra v2 token endpoint today — the gap is a worked recipe,
  not a mechanism.
- **UI auth already exists.** `ui-adapter` has
  `A case can seed a browser cookie before navigation`, explicitly motivated by
  "an E2E auth bypass." The gap is guidance that this — not IdP-login
  automation — is the recommended pattern for SSO-gated journeys.
- **Flag-proof control is a single request.** Per the archived
  `flag-proof-http-control` design (D1, D6): `flag_proof.control` is one
  `method/url/headers/body` request with `${VAR}` resolved at load and `{{…}}`
  at execution. It has no step before it, so it cannot mint a token — an
  Entra-gated flag API is currently unreachable via flag proof.

## Goals / Non-Goals

**Goals:**

- A prospect running behind VPN + Entra can get a truthful, specific answer and
  a path to a working pilot.
- Flag proof works against a flag API gated by Entra / any OAuth2 client-
  credentials flow, from case data alone.
- Keep credentials in the customer's infra — no new secret ever lands in a case
  file or the hosted platform beyond what `project-secrets` already holds.

**Non-Goals:**

- Automating interactive Entra login (MFA, Conditional Access, device
  compliance). Designed against, not deferred.
- A packaged network tunnel/agent for cloud-hosted runners.
- Dashboard SSO via the customer's tenant (that is `org-membership` territory).
- Any hosted or infra change.

## Decisions

### D1: "No inbound access" is the headline, and it is already true

The answer to the VPN half is architectural and needs no code: the customer runs
the CLI where their app is already reachable (a self-hosted runner inside the
VPN/VPC is the common case in exactly these shops), and the hosted platform only
receives an outbound HTTPS push of evidence. We document this as a first-class
selling point ("zero inbound access, zero allowlist entries, secrets never leave
your CI"), contrasted with a SaaS-crawler model that needs a reverse tunnel or
IP allowlist.

- **Alternative rejected:** build a ReleaseTwin-hosted tunnel/agent so cloud
  runners can reach isolated targets. That reintroduces the inbound-access
  problem we are advertising away from, and it is the customer's existing
  problem for any integration test — not ours to own. Deferred, not foreclosed.

### D2: Entra API auth is a documented recipe over the existing OAuth2 step

No new adapter behavior for the HTTP path. Ship a worked recipe:

```yaml
steps:
  - http.oauth2_client_credentials:
      token_url: https://login.microsoftonline.com/${AZURE_TENANT_ID}/oauth2/v2.0/token
      client_id: ${AZURE_CLIENT_ID}
      client_secret: ${AZURE_CLIENT_SECRET}
      scope: api://${AZURE_APP_ID_URI}/.default
      capture: token
  - http.request:
      headers: { Authorization: "Bearer {{token}}" }
```

The recipe also states the customer-side prerequisites plainly: an app
registration, admin consent, and an app-role assignment, all owned by the
customer's identity team; and that some orgs prohibit non-interactive service
principals for lower environments or wrap the token endpoint itself in
Conditional Access — hard blockers we cannot engineer around and should qualify
early in a sales conversation.

- **Alternative rejected:** a dedicated `http.entra_token` step. The generic
  `oauth2_client_credentials` step already does it; an Entra-specific wrapper is
  a docs concern masquerading as code.

### D3: SSO-gated UI journeys authenticate via the target app's test mode

Recommended pattern, in priority order:

1. The target app's own E2E mode — server-mints the session when a signed test
   header/cookie is present (this is the NAHA pattern: protected routes
   server-mint the API bearer in E2E mode). Seeded with `ui.setCookie`. Lowest
   fragility; the customer's app team owns it.
2. A reused `storageState` / cookie captured from a real human login, refreshed
   on a schedule. Medium fragility — sessions expire.
3. A dedicated test user that is MFA-exempt with a Conditional Access exclusion
   for the runner's egress IP. Customer identity team owns it.

We explicitly do **not** drive the interactive Entra login in Playwright.

- **Alternative rejected:** an `ROPC` (resource-owner-password) grant helper.
  Entra is deprecating ROPC and it fails outright with MFA / Conditional Access.

### D4: The flag-proof `control` block gains an optional token pre-step

This is the one real behavior change. Extend the `control` schema:

```yaml
flag_proof:
  control:
    auth:
      oauth2_client_credentials:
        token_url: https://login.microsoftonline.com/${AZURE_TENANT_ID}/oauth2/v2.0/token
        client_id: ${FLAGS_CLIENT_ID}
        client_secret: ${FLAGS_CLIENT_SECRET}
        scope: ${FLAGS_SCOPE}
    method: PUT
    url: ${FLAGS_API}/flags/{{featureKey}}
    headers: { Authorization: "Bearer {{token}}" }
    body: '{ "state": "{{state}}" }'
```

- `HttpFeatureStateController` performs the client-credentials exchange once per
  `SetStateAsync` (or caches the token for the run's two legs), then substitutes
  `{{token}}` into the toggle request alongside the existing `{{featureKey}}` /
  `{{state}}` / `{{enabled}}`.
- Reuses the HTTP adapter's existing `oauth2_client_credentials` execution path —
  no second implementation.
- A failed token exchange is a `FlagProofOutcome.ControlFailed` exactly like a
  rejected toggle (same `try/catch` in `FlagProofRunner.RunAsync` from the
  archived change's D5).
- Credentials resolve from `${VAR}` at load (env → `project-secrets`), never from
  the case file — unchanged contract.

- **Alternative rejected:** require the customer to pre-fetch the token in a CI
  step and pass `${FLAG_CONTROL_TOKEN}`. Works with zero code but pushes a
  multi-step token dance into every customer's pipeline and breaks the "flag
  proof from case data alone" promise.
- **Alternative rejected:** a general N-step `control` pipeline. Overkill; a
  single optional `auth` block covers every OAuth2-gated toggle API without
  reopening the "control is one request" simplicity.

### D5: `enterprise-access` requirements are guarantees, not mechanics

The new spec states what must remain true (the CLI runs against an isolated
target with no inbound path to the customer network; the hosted platform never
initiates a connection into a customer network; the documented Entra and
UI-test-mode auth paths exist and are exercised by an example). It does not
introduce a new execution capability — those live in `http-adapter`,
`ui-adapter`, and `http-flag-control`.

## Risks / Trade-offs

- **Customer identity team blocks non-interactive service principals** → no
  mitigation in ReleaseTwin; qualify it in the first sales/onboarding call. The
  recipe names this so it surfaces before a pilot stalls.
- **Conditional Access on the token endpoint itself** → same; only a self-hosted
  runner with an allowlisted egress IP helps, and only if the customer will add
  it.
- **App has no E2E mode for UI journeys** → the pilot starts with API + flag
  proof (which need no browser session) while the app team adds a test-mode
  session hook; UI journeys land later. Document this as the expected sequencing.
- **Token cached across the two flag-proof legs goes stale** (very short-lived
  token, slow leg) → re-exchange per `SetStateAsync` call; the exchange is one
  extra request against a fast endpoint.
- **Secret leakage via the control request** → unchanged from the archived
  change: the control request produces no evidence and its `${VAR}` values are
  already in the redactor mask list; the token is a `{{…}}` runtime value, never
  logged.

## Migration Plan

Additive and docs-led.

1. Land the docs (`enterprise-access.md`, Entra recipe, UI test-mode section,
   self-hosted-runner guidance) — no code, shippable immediately.
2. Land the `control.auth` pre-step in `HttpFeatureStateController` + the
   `flag_proof.control` parser + an Entra-gated example case.
3. Sync `enterprise-access` and the `http-flag-control` delta into main specs.

**Rollback:** the `control.auth` block is optional; a case without it behaves
exactly as the archived `flag-proof-http-control` shipped. Docs revert freely.

## Open Questions

- Is "self-hosted runner required for network-isolated targets" a plan-tier
  gate, an onboarding-activation checklist item, or purely docs? Captured here;
  does not change these specs or tasks — resolve with the plan-catalog owner.
- Should the Entra recipe also ship as a `case-scaffolding` template, or is an
  `examples/cases/` file enough for the first design partner?
