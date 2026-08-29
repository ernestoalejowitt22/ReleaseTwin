# Operator alerting

Internal reference for the operator, not customer-facing. See
`openspec/changes/archive/` (once `operator-alerting` is archived) for the full proposal/design.

## What exists

One SNS topic (`<table_prefix>releasetwin-operator-alerts`) is the single delivery channel for
everything below — one email inbox, no dashboard, no second channel.

| Trigger | What it means | Where it's defined |
|---|---|---|
| `<prefix>releasetwin-hosted-api-5xx` alarm | The hosted API returned one or more 5xx responses in the last 5 minutes | `hosted/terraform/alerting.tf` — a CloudWatch Logs metric filter over the request-completion log line `Program.cs` emits on every request (`http_request_completed status=...`), not Lambda's own `Errors` metric, which doesn't fire on a caught exception returning a clean 500 |
| `<prefix>releasetwin-hosted-api-lambda-errors` alarm | The Lambda itself crashed (unhandled exception, timeout, OOM) | Lambda's native `Errors` metric |
| `<prefix>releasetwin-hosted-api-lambda-throttles` alarm | The Lambda is being throttled by AWS Lambda concurrency limits | Lambda's native `Throttles` metric |
| Daily staleness digest | One or more projects (across every organization) have gone quiet, per the same judgment the dashboard's own staleness banner uses (`UploadStalenessCalculator`) | `<prefix>releasetwin-staleness-digest` Lambda, invoked once a day by the `<prefix>releasetwin-staleness-digest-daily` EventBridge rule |

The staleness digest is a *digest*, not a per-project alert: it lists whatever is currently stale
(possibly nothing, in which case it publishes nothing) with no "already notified" tracking — a
project stale for a week appears in the digest every day it's stale.

## One-time setup after a deploy

1. Set the `OPERATOR_ALERT_EMAIL` repo variable (Settings → Actions → Variables) before running
   `Deploy Hosted API` — left unset, the SNS topic exists with no subscription, and the digest
   Lambda logs a warning and skips publishing instead of failing.
2. After `terraform apply` creates the subscription, confirm it from the inbox at that address —
   SNS can't auto-confirm an email subscription (by design), and nothing here can automate that
   click.
3. Trigger a deliberate 5xx (a malformed authenticated request against the deployed Function URL)
   and confirm the alarm email arrives before trusting it.
4. Manually invoke the staleness digest Lambda once (`aws lambda invoke` against
   `<prefix>releasetwin-staleness-digest`) and confirm the digest email's content looks right
   before trusting the daily schedule.

## Evidence storage & purge

`dashboard-evidence-viewer` lets a Paid-tier customer opt into uploading a redacted run-evidence
document (per-step request/response summaries, assertion detail, screenshots). Two pieces of
operator infrastructure back it, wired by `evidence-purge-and-blob-store`:

| Resource | Role | Where it's defined |
|---|---|---|
| `<prefix>releasetwin-evidence-blobs` S3 bucket | Holds the redacted screenshot PNGs (one object per screenshot id). Private, SSE-S3, **no lifecycle rule** — the app deletes on each project's own retention window, not a fixed age. | `hosted/terraform/evidence.tf` |
| `<prefix>releasetwin-evidence-purge` Lambda | Runs `EvidencePurgeService` once a day via `<prefix>releasetwin-evidence-purge-daily` EventBridge rule — deletes every evidence document (and its blobs) older than its project's `EvidenceRetentionDays` (default 30, max 365), leaving the metadata report row untouched. | `hosted/terraform/evidence.tf` |

Same "second Lambda sharing the HTTP function's artifact, discriminated by `RELEASETWIN_LAMBDA_TASK`"
pattern as the staleness digest — except the purge role is read-**write** on the table
(`dynamodb:DeleteItem`), since it removes expired rows. The API function sets `Evidence__BlobBucket`,
which is what switches its blob store from the local-dev filesystem to S3.

**One-time setup:** after `terraform apply` creates the bucket + purge Lambda, redeploy the API
package so it picks up `Evidence__BlobBucket`, then manually invoke the purge Lambda once
(`aws lambda invoke` against `<prefix>releasetwin-evidence-purge`) and confirm its log line
(`evidence_purge_run purged_count=...`) before trusting the daily schedule.

## Why this shape

See `design.md`'s Decisions for the full reasoning; the short version: single operator, single
channel, no suppression/snooze logic, no general observability stack — this closes a specific,
concrete gap (nothing told the operator when the API broke or a customer went quiet), nothing more.
