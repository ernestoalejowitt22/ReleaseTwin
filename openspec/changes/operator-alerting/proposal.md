## Why

Nothing today tells the operator when the hosted platform itself is broken or when a customer's
uploads have gone quiet. `upload-staleness` already judges a project stale from its own upload
history, but only when someone opens the dashboard — a customer who stops running the CLI (or a
customer's org that never comes back) is invisible unless they happen to log back in and see the
banner themselves. Confirmed: the deployed Lambda has zero CloudWatch alarms today (checked
`hosted/terraform/lambda.tf` — logging is wired up, nothing watches it).

## What Changes

- A CloudWatch metric filter over the Lambda's own request logs, matching 5xx responses, feeding a
  CloudWatch Alarm — Lambda's own `Errors` metric only fires on an unhandled exception escaping the
  function; ASP.NET catches most failures and returns a clean 500 that never trips it, so the
  log-based filter is the real signal for "the API is returning errors."
- A second CloudWatch Alarm on the Lambda's own `Throttles`/`Errors` metrics, for the failures that
  do escape the handler (cold-start crash, timeout, out-of-memory).
- One SNS topic (`releasetwin-operator-alerts` or similar) with an email subscription to the
  operator's own address, as the single delivery channel for everything in this change.
- A new, small scheduled entry point (EventBridge cron, daily) that runs the existing
  `upload-staleness` judgment across every project server-side (reusing the same judgment logic the
  dashboard banner already computes client-side per project) and, if one or more projects are
  currently stale, publishes a single digest to the same SNS topic — not a per-project alert, and
  not re-alerting hourly on the same stale project.

## Capabilities

### New Capabilities

(none — this only adds operator-facing infrastructure and a scheduled re-run of an existing,
already-specified judgment; no customer-facing behavior or requirement is introduced. `skip_specs:
true` set in `.openspec.yaml`, matching the precedent set by `hosted-platform-deployment`.)

### Modified Capabilities

(none — reuses `upload-staleness`'s existing judgment logic without changing its requirements; this
change adds a second, operator-facing consumer of that judgment, not a new rule.)

## Impact

- New: two CloudWatch Alarms + one CloudWatch Logs metric filter, defined in
  `hosted/terraform/lambda.tf` (or a new `hosted/terraform/alerting.tf`).
- New: one SNS topic + email subscription, in the same terraform.
- New: a scheduled entry point in `ReleaseTwin.Hosted.Api` (or a small sibling Lambda sharing its
  DynamoDB access) triggered by a new EventBridge rule, plus the terraform wiring that rule.
- No changes to any existing hosted API request/response contract, the CLI, or `ReleaseTwin.Core`.
- Real (if small) new AWS cost: SNS is effectively free at this volume; CloudWatch Alarms are a few
  cents/month each; the scheduled Lambda invocation is a single daily call, well within free tier.
