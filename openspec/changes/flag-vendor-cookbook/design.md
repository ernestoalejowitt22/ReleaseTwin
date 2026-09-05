## Context

See proposal.md - Why for the "verified, not just typed" standard this change holds
itself to. Both target vendors were stood up as real, local Docker instances and
driven with real HTTP calls (`curl`, then the actual `releasetwin` CLI) before any
recipe was written down — this is a record of what that testing found, not a plan for
what to build.

## Goals / Non-Goals

**Goals:**
- Every recipe in `docs/flag-proof.md`'s cookbook reflects a vendor's real API, checked
  against a live instance.
- The cookbook's framing leads with the most common real case (a homegrown,
  database-backed flag) rather than implying a named vendor is required.

**Non-Goals:**
- No attempt to make every open-source flag vendor fit `control`'s token model by
  changing the engine — that's a separate, larger decision (see Decisions below).
- No coverage of GrowthBook's targeting rules or percentage rollouts — flag proof only
  needs the boolean on/off case.

## Decisions

**Unleash dropped, not worked around.** Initial investigation (from documentation
knowledge, not yet verified) assumed Unleash's Admin API supported a JSON-Patch `PATCH
.../environments/<env>` request with an `enabled` boolean — this seemed reasonable and
would have fit `control`'s model (`body: '[{"op":"replace","path":"/enabled","value":
{{enabled}}}]'`). Standing up a real Unleash instance (`unleashorg/unleash-server` +
Postgres via Docker Compose) and checking its live OpenAPI spec
(`/docs/openapi.json`) showed this endpoint doesn't exist — only `GET` is defined on
that path. The only way to toggle a feature's environment state is two dedicated
action endpoints: `POST .../environments/<env>/on` and `.../off` (both confirmed
working with real calls). Checked every other schema on the instance for a
body-based alternative (top-level feature `PATCH`/`PUT`, bulk-features endpoints) —
none exposes `enabled` as a settable field anywhere but through those two endpoints.

`control`'s only per-leg-varying values are `{{state}}` (`enabled`/`disabled`) and
`{{enabled}}` (`true`/`false`) — neither renders `on`/`off`, and nothing in the
`control` schema lets a case choose between two different URLs for the two legs.
Alternative considered: extend the engine with a `state_values` mapping (e.g.
`{enabled: "on", disabled: "off"}`) so a case could declare vendor-specific URL/body
tokens. Rejected for this change specifically — it's a real engine capability with its
own design questions (does it also need method-level branching? header branching?),
not a docs/examples change, and `.openspec.yaml` here declares `skip_specs: true`. If
a specific customer or a strong OSS-adoption signal asks for Unleash support, that's
its own proposal.

**GrowthBook fits cleanly, once its true endpoint was found.** The first attempt to
match memory of GrowthBook's API (`POST .../features/{id}/toggle`) didn't exist either
— confirmed via a live 404 against the real instance. The actual mechanism: `POST
/api/v1/features/{id}` (no environment/state in the URL) with a body of
`{"defaultValue": "<string>"}` updates the flag's value across every environment in one
call. `defaultValue` is strictly a JSON string, not a boolean (`{"defaultValue": true}`
is rejected with a schema error) — this is why the recipe's body template is `'{
"defaultValue": "{{enabled}}" }'` with the token inside literal quotes rather than
bare; `{{enabled}}` substitutes the text `true`/`false` into that position, producing a
valid JSON string. This was verified with the real `releasetwin` CLI, not just curl:
`FLAGPROOF FLAGPROOF-GROWTHBOOK-DEMO-1 (Passed)` against the live container.

**GrowthBook's account/API-key setup has no headless seed, unlike Unleash's
`INIT_ADMIN_API_TOKENS`.** A first-run GrowthBook instance requires signing up through
its web UI (company/name/email/password) before any API key can exist — there is no
environment variable or CLI flag to skip this. The example's README documents this as
a one-time step; it isn't something the `docker compose` file can automate away.

**The compose file lives in a `docker/` subdirectory, not the example's root.** The
CLI's case loader treats every `.yaml`/`.yml` file in a cases directory as a case file
(confirmed by trying it: `docker-compose.yml` at the example's root produced "Failed
to load cases: docker-compose.yml: missing required field 'id'"). Moving it to
`docker/docker-compose.yml` keeps it out of the scan.

## Risks / Trade-offs

- [A reader assumes Unleash isn't supported at all, rather than "not via one shared
  `control` block today"] → the cookbook's closing note names the specific mechanism
  gap and the real fix (a `state_values`-style engine change), so the limitation reads
  as precise and bounded, not a vague "doesn't work."
- [GrowthBook's or Unleash's real API changes in a future version, silently
  invalidating this recipe] → same exposure every other "verified live" claim in this
  repo already carries (e.g. the LaunchDarkly e2e, the CI-portability screenshots); no
  different risk profile introduced here.

## Migration Plan

None — purely additive, no existing behavior changes.
