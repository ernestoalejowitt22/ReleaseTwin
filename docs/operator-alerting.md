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

## Why this shape

See `design.md`'s Decisions for the full reasoning; the short version: single operator, single
channel, no suppression/snooze logic, no general observability stack — this closes a specific,
concrete gap (nothing told the operator when the API broke or a customer went quiet), nothing more.
