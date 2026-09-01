## 1. Flag-proof control auth (code)

- [x] 1.1 Extend the `flag_proof.control` parser in `ReleaseTwin.Cli` case loading to accept an optional `auth.oauth2_client_credentials` block (`token_url`, `client_id`, `client_secret`, optional `scope`); validate it the same way `http.request` / existing `control` params are validated, with a clear case-load error on a malformed block — `FlagProofControlAuthDto` / `FlagProofOauth2ClientCredentialsDto` + `ResolveFlagProofControlAuth`
- [x] 1.2 Resolve the `auth` block's `${ENV_VAR}` references at load time via the existing env → `project-secrets` interpolation, so the parsed block holds no literal credential
- [x] 1.3 In `HttpFeatureStateController`, when an `auth` block is present, perform the client-credentials exchange before the toggle request, and substitute the captured token for `{{token}}` alongside `{{featureKey}}` / `{{state}}` / `{{enabled}}` — `HttpFlagAuth` + `ExchangeTokenAsync`
- [x] 1.4 The exchange runs **per leg** (spec: "before performing the control request for each leg") — no token caching / expiry logic, since the two legs are seconds apart and a fresh token is simpler and always valid. A failed exchange throws `FlagControlException`, which `FlagProofRunner` already maps to `FlagProofOutcome.ControlFailed`
- [x] 1.5 The controller emits no evidence buffer and the token/secret never reach a log; token-exchange failure messages carry only method + URL + status, never the response body

## 2. Flag-proof control auth (tests)

- [x] 2.1 `CaseFileLoaderTests`: `FlagProofControlAuthBlockRoundTrips...`, `...WithoutClientSecretIsRejected`, `...WithoutOauth2BlockIsRejected`
- [x] 2.2 `HttpFeatureStateControllerTests.AuthMintsTokenPerLegAndSubstitutesIntoControlRequest` (+ `AuthOmitsScopeFromFormWhenNotDeclared`)
- [x] 2.3 `AuthTokenEndpointFailureThrowsFlagControlExceptionWithoutLeakingSecret` + `AuthResponseWithoutAccessTokenThrowsFlagControlException` (control request never sent); `FlagProofRunnerTests.ThrowingControllerIsControlFailedNotIneligible` already covers the runner-level `ControlFailed` mapping
- [x] 2.4 `HttpFeatureStateControllerTests.NoAuthSectionSendsNoTokenRequest`

## 3. Example case

- [x] 3.1 `examples/cases/enterprise/example-flag-proof-http-entra.yaml` (+ `examples/fixtures/example-flag-proof-http-entra.json`) — flag-proof `control` block with `auth.oauth2_client_credentials` against the Entra v2 token endpoint, toggling with `Bearer {{token}}`
- [x] 3.2 `examples/cases/enterprise/example-entra-api-auth.yaml` (+ fixture) — `http.oauth2ClientCredentials` → `http.request` API-auth case, referenced by `docs/enterprise-access.md`
  - Placed under `examples/cases/enterprise/` (a subdirectory the batch loader's `TopDirectoryOnly` scan skips) because these require real credentials + network to run, and `ExampleCaseEndToEndTests` runs every case in `examples/cases/` green. `ExampleCaseEndToEndTests.EntraExampleCasesLoadWithEveryCredentialResolvedFromTheEnvironment` load-verifies both (parse + fixture hash + no literal credential)

## 4. Enterprise-access documentation

- [x] 4.1 `docs/enterprise-access.md` — "no inbound access" architecture (diagram), self-hosted-runner table for isolated targets, cloud-runner / customer-tunnel caveat
- [x] 4.2 Entra API-auth recipe (worked `http.oauth2ClientCredentials` snippet, v2 token endpoint, `.default` scope), identity-team prerequisites, and the qualifying blockers (SP prohibited in lower envs, Conditional Access on the token endpoint)
- [x] 4.3 SSO-gated UI-journey section — app test mode via `ui.setCookie` first, then reused `storageState`, then MFA-exempt test user + CA egress-IP exclusion; explicit "do not script the IdP login"
- [x] 4.4 Conditional-Access-by-IP note (self-hosted runner egress IP → Entra trusted location; cloud runners cannot)
- [x] 4.5 Linked from `docs/quickstart.md` (More list) and `docs/flag-proof.md` (new `control.auth` section)

## 5. Validation & spec sync

- [x] 5.1 `dotnet build ReleaseTwin.sln` clean; `dotnet test ReleaseTwin.sln` green — 253 passed, 0 failed (Core 49, Http 29, LaunchDarkly 5, AzureDevOps 12, AdapterSdk 10, Cli 135, Ui 13)
- [x] 5.2 `openspec validate enterprise-network-and-sso --strict` passes
- [ ] 5.3 After approval, sync `enterprise-access` (new main spec) and the `http-flag-control` delta into `openspec/specs/` — **do at archive time, not now**

## 6. Deferred (not this change — see design Open Questions)

- [ ] 6.1 Decide with the plan-catalog owner whether "self-hosted runner required for network-isolated targets" is a plan-tier gate, an onboarding-activation checklist item, or docs only
- [ ] 6.2 Decide whether the Entra recipe also ships as a `case-scaffolding` template
