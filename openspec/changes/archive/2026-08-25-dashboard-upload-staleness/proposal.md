## Why

A project that was uploading case reports normally can go silent — a workflow refactor drops the
`RELEASETWIN_API_TOKEN` export, a secret expires, a CI job gets disabled — and nothing in the
product ever says so. The CLI is stateless per run and can't tell "never configured" from "used to
work, now doesn't," so this can only be caught from the upload history the backend already has.
Today a customer only notices by manually comparing dates in run history, if they think to look at
all.

## What Changes

- Add a per-project staleness rule: using a project's own upload history (case reports and
  flag-proof reports combined), compute the typical gap between uploads and flag the project when
  the time since its most recent upload exceeds a multiple of that typical gap.
- A project needs a minimum amount of upload history before it's eligible to be judged stale —
  a newly onboarded project with too few uploads to establish a baseline is left alone (that's the
  "never configured" problem, not this one).
- Surface the flag on the dashboard as a visible banner on the affected project, alongside the
  existing run history.
- No new outbound notification (email, Slack, etc.) and no per-project dismiss/mute — the banner
  shows whenever the condition holds and disappears once a new upload arrives. Both are explicitly
  out of scope for this change.

## Capabilities

### New Capabilities
- `upload-staleness`: defines what "stale" means for a project (minimum history required, typical
  gap computed from that project's own upload timestamps, threshold multiplier) and exposes that
  judgment for a given project's upload history.

### Modified Capabilities
- `dashboard`: gains a requirement that the dashboard shows a staleness banner for the selected
  project when `upload-staleness` judges it stale.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/Services/DashboardService.cs` and its `DashboardView` — needs to
  compute and expose the staleness judgment for the selected project alongside the data it already
  loads (`caseReports`, `flagProofReports`).
- New domain logic (a small, independently testable staleness calculator) rather than a change to
  ingest or storage — no new entity fields or migrations, since the timestamps it needs
  (`UploadedCaseReport.UploadedAt`, `UploadedFlagProofReport.UploadedAt`) already exist.
- `web/src/app/dashboard` — a banner UI element on the project view.
