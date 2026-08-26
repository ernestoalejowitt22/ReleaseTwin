## Why

`dashboard-staleness-banner.cy.ts` (from the archived `dashboard-upload-staleness` change) seeds its
upload history via a dev-only `/dev/seed-case-report-history` endpoint that writes directly to the
database, bypassing the real CLI/ingest path entirely — added only because the real ingest endpoint
always stamps `UploadedAt` with `DateTimeOffset.UtcNow`, so backdated history seemed unreachable any
other way. This was flagged as a known gap to fix, matching the same "no seeded data, everything
real" standard `e2e-github-connection-flow` and `github-oauth-private-repos` were held to.

## What Changes

- Replace the seeded-history approach with a real one: `upload-staleness`'s rule is a ratio (gap >
  3× typical gap), not an absolute duration, so a tight, real, few-seconds cadence established by
  actually running the CLI repeatedly proves the same logic as a multi-day cadence would, with real
  wall-clock waiting instead of backdated timestamps.
- Remove the `/dev/seed-case-report-history` endpoint (`Program.cs`) — no longer needed, and its
  removal is itself in the spirit of the "compute-on-read, no new infra" design already established
  for the staleness feature.
- Add a small dedicated example case (`examples/cases-http-only/`) so each CLI invocation produces
  exactly one upload — the existing `examples/cases` bundle uploads two per run, which would muddy
  the controlled cadence this test needs.

## Capabilities

No spec-level behavior changes — this replaces one test's data-setup mechanism with another;
`upload-staleness` and `dashboard`'s actual requirements (already specified) are unchanged.
`.openspec.yaml` sets `skip_specs: true`.

## Impact

- `web/cypress/e2e/dashboard-staleness-banner.cy.ts`: rewritten to drive real CLI runs instead of
  calling the seeding endpoint.
- `hosted/ReleaseTwin.Hosted.Api/Program.cs`: removes the dev-only seeding endpoint.
- `examples/cases-http-only/`: new, minimal example case directory (reuses the existing
  `examples/fixtures/example-http.json` fixture via the CLI's default fixtures-root convention).
