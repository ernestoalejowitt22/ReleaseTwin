## Why

A recurring enterprise objection: "everything is behind our VPN and gated by
organization Microsoft (Entra ID) OAuth." Today we have no crisp answer, even
though the CLI-in-your-infra architecture already solves the hard half (no
inbound network access is ever required). The auth half is mostly a
documentation gap — the HTTP adapter's OAuth2 client-credentials step and the
UI adapter's cookie/session seeding already cover the mechanisms — plus one real
behavior gap: a flag-proof `control` request cannot mint its own token, so an
Entra-gated flag API is unreachable.

## What Changes

- **Enterprise access guidance (docs).** A single reference covering how
  ReleaseTwin runs against a VPN-isolated, SSO-gated target:
  - **Network:** the engine runs in the customer's CI on their runners; the
    hosted platform is ingest-only and never connects into their network. For a
    network-isolated target, a self-hosted runner (inside the VPN / VPC) is the
    supported path; cloud-hosted runners need a tunnel the customer owns.
  - **API auth (Entra):** `http.oauth2_client_credentials` against
    `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token` with
    `scope=api://<app>/.default`, token captured and sent as a bearer — a
    worked recipe, not just a spec sentence. Requires a customer app
    registration + admin consent + app-role assignment (their identity team).
  - **UI-journey auth:** the recommended pattern is the target app's own E2E
    mode (server-mints the session when a signed test header/cookie is present),
    seeded via the existing `ui.setCookie` step — not automating the interactive
    Entra login with MFA / Conditional Access, which is brittle and often
    against customer policy.
  - **Conditional Access by IP:** a self-hosted runner has a stable egress IP
    that can be added to Entra trusted locations; cloud runners cannot.
- **Flag-proof `control` block can acquire a token.** The `control` request
  gains an optional client-credentials pre-step (or `auth:` shorthand) so a
  flag system behind Entra / any OAuth2-gated toggle API is reachable, resolving
  client id/secret from `${ENV_VAR}` / project secrets exactly as the toggle
  request already does.
- **Plan-tier note (docs only).** Whether "self-hosted runner required for
  network-isolated targets" is a plan-tier or onboarding concern — captured, not
  decided here.

## Capabilities

### New Capabilities

- `enterprise-access`: how ReleaseTwin operates against a target that is
  network-isolated (VPN/VPC) and identity-gated (Entra ID / org OAuth) — runner
  placement expectations, the Entra client-credentials auth path, UI-journey
  auth via the target app's test mode, and Conditional Access egress-IP
  guidance. Requirements here are about what the engine and its docs must
  guarantee, not new execution mechanics.

### Modified Capabilities

- `http-flag-control`: the `control` request MAY obtain its own access token
  via an OAuth2 client-credentials exchange before performing the toggle, so a
  flag API gated by Entra / org OAuth is reachable from case data alone, with
  credentials still resolved from `${ENV_VAR}` / project secrets.

## Impact

- **docs:** new `docs/enterprise-access.md` (or a section in `quickstart.md`);
  an Entra recipe in the HTTP-adapter docs; a UI-journey "authenticate via your
  app's test mode" section; self-hosted-runner guidance.
- **`ReleaseTwin.Adapters.Http` / `ReleaseTwin.Cli`:** `HttpFeatureStateController`
  and the `flag_proof.control` parser gain an optional `auth` /
  client-credentials pre-step; reuses the HTTP adapter's existing
  `oauth2_client_credentials` code path.
- **`examples/cases/`:** an Entra-gated HTTP flag-proof example.
- **no hosted change, no infra change.**

## Explicitly deferred

- Enterprise SSO *into the ReleaseTwin dashboard* via the customer's Entra
  tenant — separate from testing their app; belongs with `org-membership`.
- Automating the interactive Entra login (MFA, Conditional Access, device
  compliance) — explicitly not pursued; the app-test-mode pattern is the
  recommendation.
- ROPC (resource-owner-password) grant — Entra is deprecating it; do not build
  on it.
- A packaged tunnel/agent for cloud-hosted runners to reach isolated targets.
