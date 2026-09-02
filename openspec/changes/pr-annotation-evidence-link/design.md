## Context

See proposal.md — Why. `RunSummary` (`src/ReleaseTwin.Cli/RunSummary.cs`) is
`schemaVersion` 1: `overall`, `totals`, `flagProof`, `cases[]`. `render.mjs`
(Apache-2.0) reads it and writes a marker-keyed PR comment + a `ReleaseTwin`
check run. `CliRunner.cs` uploads via `IngestClient` when `RELEASETWIN_API_TOKEN`
is set; the ingest response is currently not inspected for a URL. `IngestClient`
already distinguishes "report stored / evidence not accepted".

## Goals / Non-Goals

**Goals:**
- A reviewer on the PR can click through to the run and to a failed case's
  evidence — when a hosted project exists.
- The no-account CI path is unchanged except the schema version integer.

**Non-Goals:**
- A legacy commit status via the Statuses API (check runs already gate).
- Rendering any evidence content in the PR.
- Changing the ingest *request* payload or redaction.

## Decisions

### `schemaVersion` → 2; new fields are additive and nullable
`runUrl` (top level) and `evidenceUrl` (per `RunSummaryCase`), both omitted when
null. **Alternative rejected:** a side-channel file — one summary artifact is the
contract `render.mjs` consumes; splitting it invites drift.

### The hosted ingest response returns the canonical report URL
The CLI does not construct dashboard URLs — it echoes what ingest returns. Keeps
URL shape owned by the hosted app, and means an on-prem/self-host base URL is
handled server-side. **Alternative rejected:** CLI builds `${API_URL}/...` — bakes
the dashboard route into the CLI and breaks when the web app's routing changes.

### `render.mjs`: link the comment header + set `details_url`; link failed rows
`details_url` on the check run is the native "click through" affordance. The
comment header gets a "View run" link. Case rows already exist for notable cases;
a row gets wrapped in a link when `evidenceUrl` is set. No new Action inputs.

### Absence is the norm, not an error
No token → no upload → no URLs → today's output. `render.mjs` guards every URL
render on presence. A v1 summary (old CLI, new Action) also just works.

## Risks / Trade-offs

- **A stale/incorrect URL from a hosted bug points reviewers nowhere.** → URL is
  server-owned and covered by hosted tests; the link is additive, the verdict is
  still in the comment text.
- **schemaVersion bump breaks a strict consumer.** → The only known consumer is
  `render.mjs`, updated here; `docs/ci.md` documents the field as optional and
  the version as forward-compatible.
- **Evidence URL implies evidence exists.** → Only set when the upload's evidence
  was accepted; not set on the not-accepted path.

## Migration Plan

Additive. Old CLI + new Action: no URLs, renders as before. New CLI + old Action:
extra JSON fields ignored. New both, no token: unchanged output. New both, token:
links appear.

## Open Questions

- Should `runUrl` also be surfaced in the CLI's own stdout at end-of-run (not
  just the JSON)? Minor, could fold in.
- One `runUrl` per invocation assumes one hosted run per CLI invocation — confirm
  the ingest model groups an invocation's uploads under one run page.
