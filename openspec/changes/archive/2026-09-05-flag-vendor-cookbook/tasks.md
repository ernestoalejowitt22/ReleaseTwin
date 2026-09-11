## 1. Unleash — investigated, dropped

- [x] 1.1 Stood up a real Unleash instance (`unleashorg/unleash-server` + Postgres via
      Docker Compose) and wrote an initial case using a `PATCH .../environments/<env>`
      JSON-Patch body.
- [x] 1.2 Verified against the live instance and its own OpenAPI spec: that endpoint
      doesn't exist (`GET`-only). The real toggle mechanism is two dedicated action
      endpoints (`.../on`, `.../off`) with no body-based alternative anywhere in the
      Admin API — confirmed by checking every relevant schema, not assumed.
- [x] 1.3 Concluded `control`'s fixed token vocabulary (`{{state}}`, `{{enabled}}`)
      cannot select between two different URLs, so Unleash cannot be represented by
      one shared `control` block today. Removed the in-progress Unleash example
      (case, fixture, compose file) rather than ship something that doesn't work; see
      design.md - Decisions.

## 2. GrowthBook example

- [x] 2.1 `examples/cases-flag-proof-growthbook/docker/docker-compose.yml` — MongoDB +
      GrowthBook (moved to a `docker/` subdirectory so the CLI's case loader, which
      treats every `.yaml`/`.yml` in a cases directory as a case, doesn't try to load
      it).
- [x] 2.2 One-time account/API-key setup done via the running instance's UI (no
      headless seed exists for GrowthBook, unlike Unleash) to obtain a real Admin
      secret key for verification.
- [x] 2.3 Found GrowthBook's real toggle mechanism empirically (the initially assumed
      `.../toggle` endpoint doesn't exist either): `POST /api/v1/features/{id}` with
      `{"defaultValue": "<string>"}` — a JSON *string*, confirmed by a rejected
      unquoted-boolean request.
- [x] 2.4 `examples/cases-flag-proof-growthbook/flag-proof-growthbook.yaml` + fixture —
      toggles via the real endpoint, `{{enabled}}` substituted inside quotes in the
      body to produce a valid JSON string.
- [x] 2.5 `examples/cases-flag-proof-growthbook/README.md` — run it end to end
      (compose up, one-time sign-up, create the feature, run the case), plus the
      vendor-neutral recipe for pointing at your own instance.
- [x] 2.6 Verified with the real `releasetwin` CLI against the live container (not
      just curl): `dotnet run --project src/ReleaseTwin.Cli --
      examples/cases-flag-proof-growthbook` → `FLAGPROOF FLAGPROOF-GROWTHBOOK-DEMO-1
      (Passed)`, `1 passed, 0 failed`.

## 3. Documentation

- [x] 3.1 Rewrote `docs/flag-proof.md`'s "Vendor cookbook" section: leads with the
      "your own database-backed flag endpoint" case (linking the already-verified
      Express demo), the GrowthBook recipe, and a closing note explaining why Unleash
      isn't listed (the real mechanism gap, not an oversight).
- [x] 3.2 Updated `README.md`'s examples section with the GrowthBook example, matching
      the existing prose style (not a literal table).

## 4. Verification

- [x] 4.1 `openspec validate flag-vendor-cookbook --strict` passes.
- [x] 4.2 Docker containers (Unleash+Postgres, GrowthBook+Mongo, and the ad hoc
      scratchpad instance used before the final directory existed) torn down after
      verification — nothing left running.
