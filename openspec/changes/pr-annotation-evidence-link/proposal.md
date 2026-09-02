## Why

The PR annotation Action already renders a comment **and** a check run — the
"status check" half of this backlog item is done. What is missing is the **link
back to the evidence**: the comment and check show `N passed, M failed` and the
flag-proof verdict, but a reviewer who wants to see *why* a case failed — the
redacted evidence bundle on the dashboard — has no way to get there from the PR.
When a run uploaded to a hosted project, that evidence exists and is one click
away in the dashboard; the PR just doesn't say so.

The blocker is plumbing: the ingest API does not tell the CLI where the uploaded
report landed, and the machine-readable run summary (`RunSummary`, `schemaVersion`
1) has no field for a URL. So the Action has nothing to render even when a
dashboard page exists.

## What Changes

- **Ingest response carries a canonical URL.** A successful ingest upload of a
  case or flag-proof report SHALL return the dashboard URL for that report (and,
  where evidence was accepted, it resolves to the evidence view). The URL is
  org-scoped and carries no sensitive content — same guarantee as the case
  identifier.
- **`RunSummary` schema v2.** A new optional top-level `runUrl` (the run's
  dashboard page) and an optional per-case `evidenceUrl`. `schemaVersion` bumps
  to `2`; a v1 consumer ignoring unknown fields is unaffected, and the CLI writes
  no URL fields when there was no upload (the no-account path is byte-for-byte
  unchanged except the version integer).
- **The CLI populates them.** When `RELEASETWIN_API_TOKEN` / `RELEASETWIN_API_URL`
  are set and an upload succeeds, the CLI records the returned URL(s) into the
  summary. An upload failure or a not-accepted evidence tier leaves the fields
  unset — consistent with "upload failure is a warning, not a case failure".
- **The Action renders the links.** `render.mjs`: when the summary carries
  `runUrl`, the comment header links "View run" and the check run's `details_url`
  points at it; when a failing case row carries `evidenceUrl`, that row links to
  the evidence. When neither is present (no-account CI), the output is exactly as
  today.
- **Docs.** `docs/ci.md` and the Action README note that setting the API token
  additionally turns the PR annotation into a link into the dashboard.

Non-goals:

- **A legacy commit status (Statuses API).** The check run already satisfies
  GitHub required-status-checks on a protected branch; adding a parallel commit
  status is redundant surface. Revisit only if a customer's branch protection is
  pinned to a status context.
- Rendering evidence content in the PR itself — the redaction guarantee is that
  evidence leaves the runner only to the hosted store; the PR gets a link, not
  the bundle.
- Any change to what evidence is captured or redacted.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ingest-api`: new requirement — a successful upload response returns the
  org-scoped dashboard URL for the stored report, subject to the same
  "no sensitive content" guarantee as the identifier fields.
- `ci-pr-integration`: the run summary MAY carry a run URL and per-case evidence
  URLs; when present the Action renders them as links in the comment and sets the
  check run's details URL; when absent the rendered output is unchanged.

## Impact

- **`src/ReleaseTwin.Cli/RunSummary.cs`** — `schemaVersion` → 2; `runUrl` +
  per-case `evidenceUrl` (both nullable, omitted when null).
- **`src/ReleaseTwin.Cli/Upload/IngestClient.cs`** — parse the URL from the
  ingest response; **`CliRunner.cs`** — thread it into `RunSummaryBuilder`.
- **`hosted/`** — ingest endpoint returns the canonical report URL in its
  response body (BSL side).
- **`integrations/github-action/render.mjs`** — link rendering; `action.yml`
  unchanged. Apache-2.0 side.
- **Docs** — `docs/ci.md`, `integrations/github-action/README.md`.
- No change to exit codes, redaction, or the ingest *request* contract.
