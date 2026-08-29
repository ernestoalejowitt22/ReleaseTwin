## Context

See proposal.md - Why. Two relevant facts about the current implementation shape this design:

- `UploadStalenessCalculator.IsStale` (`hosted/ReleaseTwin.Hosted.Api/Services/UploadStalenessCalculator.cs`)
  is already a pure, server-side function over a project's upload timestamps — `DashboardService`
  calls it inline when rendering the dashboard. There is no client-side duplication to worry about;
  a scheduled job can call the exact same function.
- The DynamoDB table is single-table, keyed per-organization (`Keys.Org(organizationId)`).
  `ProjectRepository` only exposes `ListByOrganizationAsync` — there is no existing "list every
  project across every organization" query. At the scale this product operates at today (single-digit
  customers), an occasional full-table `Scan` filtered to `PROJECT#` items is an acceptable,
  honest solution — not a query pattern to build indexes for prematurely.
- Deployment is Lambda behind a Function URL (`hosted-platform-deployment`), terraform-managed in
  `hosted/terraform/`. There is no existing scheduled/cron infrastructure anywhere in the stack.

## Goals / Non-Goals

**Goals:**
- The operator gets an email when the hosted API is returning errors.
- The operator gets a daily digest email when one or more projects are currently stale.
- Everything ships through the same terraform-managed AWS account already in use — no new AWS
  service category, no new SaaS dependency.

**Non-Goals:**
- No customer-facing notification (a customer is never emailed about their own staleness by this
  change — that would be a real scope expansion of `upload-staleness`, not touched here).
- No general-purpose metrics dashboard, tracing, or APM. This is alerting only.
- No per-project alert dedup/suppression logic beyond "one alarm state" for API health and "one
  digest a day" for staleness — no snoozing, no severity tiers, no on-call rotation. Single operator,
  single channel.
- No alerting on DynamoDB-level metrics (throttling, capacity) — Lambda-level errors/throttles are
  the practical proxy for now; revisit only if a real incident shows this gap matters.

## Decisions

**5xx detection: log-based metric filter, not the Lambda `Errors` metric.**
Lambda's own `Errors` metric increments only when the function itself throws an exception that
escapes the handler entirely. ASP.NET's exception handling middleware (`UseExceptionHandler`,
already wired in `Program.cs`) catches nearly everything and returns a clean HTTP 500 — which
Lambda considers a *successful* invocation. A CloudWatch Logs metric filter matching the request
logging middleware's own status-code output is the only signal that actually reflects "customers
are seeing errors." Alternative considered: emit a custom EMF (embedded metric format) metric
from application code on every 5xx — more precise, but adds application-level instrumentation code
for a problem the existing structured request logs already solve via a metric filter; deferred
unless the log-filter approach proves too noisy or imprecise in practice.

**Escaped-exception alarm: Lambda's own `Errors`/`Throttles` metrics, unchanged.**
These are free (no extra logging/filter needed) and catch exactly the failure class the 5xx filter
can't see — a crash before the app ever produces a response, a timeout, an out-of-memory kill.
Both alarms feed the same SNS topic; two alarms, one topic, one inbox.

**Delivery: one SNS topic, one email subscription.**
No Slack, no PagerDuty, no second channel — single operator, and email is a real, durable channel
already used everywhere else operator-facing (AWS's own account notifications, GitHub, etc.).
Subscription confirmation is a one-time manual click in the operator's inbox after `terraform
apply` — documented in the migration plan below, not automated (SNS doesn't support
auto-confirming an email subscription, by design, to prevent spam).

**Staleness digest: a scheduled entry point in the existing Lambda, not a second Lambda.**
`AddAWSLambdaHosting(LambdaEventSource.HttpApi)` already switches the app's hosting model based on
the Lambda event source at runtime. Rather than stand up a second deployable, the same handler can
branch on event source (EventBridge Scheduled Event vs. HTTP API) and run a small scan-and-judge
routine instead of serving a request. This keeps deployment topology exactly as simple as it is
today — one Lambda, one artifact, one set of environment variables — at the cost of a small
branch in the entry point. Alternative considered: a second, dedicated Lambda function — rejected
for now as unnecessary duplication of deployment config (env vars, IAM role, VPC settings if any)
for a single daily invocation; revisit if the entry-point branching gets unwieldy.

**Full-table Scan for cross-organization project listing.**
`ProjectRepository` gains a new `ListAllAsync` (or the scheduled routine queries `IHostedTable`
directly) that Scans the table filtered to `PROJECT#` sort-key items, since no per-org partition
key is available up front in this job. Explicitly not optimized — a Scan is the honest, simplest
answer given the sole caller runs once a day against a table sized for a handful of customers.
Flagged here so it isn't mistaken for an oversight if the table grows.

**Digest, not per-project alert, and no dedup state.**
The scheduled job re-evaluates staleness fresh every run and emails a single digest listing
*currently* stale projects (possibly empty — a run with nothing stale sends nothing, not an empty
"all clear" email). Because it runs once daily and lists current state rather than newly-crossed
state, no "already notified" tracking is needed — accepted trade-off: if a project is stale for a
week, the operator gets the same project listed in the digest for a week. That's a feature, not a
bug, for the single-operator case — a silent, stale dedup table would be the wrong kind of "quiet."

## Risks / Trade-offs

- [Log-based metric filter could be noisy on non-critical 4xx-adjacent conditions if the request
  logging middleware's format changes] → Metric filter pattern is scoped to matching `5` in the
  status-code field specifically, not a generic error-keyword match; a middleware format change
  would need the filter pattern updated alongside it — call this out explicitly in the task that
  adds the filter.
- [A full-table Scan run daily could get slow/costly if the table grows to hundreds of orgs] →
  Explicitly out of scope to optimize now (see Decisions); a future change can add a GSI if this
  job's own CloudWatch duration metric shows it becoming a real problem.
- [Digest emails could become "the boy who cried wolf" if a project is expected to go stale
  (seasonal customer, paused pilot)] → No suppression mechanism in this change (Non-Goals); if this
  becomes a real annoyance in practice, a follow-up change can add an explicit
  "pause staleness alerts for this project" control, informed by real usage rather than speculation.

## Migration Plan

1. Add the SNS topic + email subscription in terraform, apply, then manually confirm the
   subscription from the operator's inbox (one-time, out-of-band step — cannot be automated).
2. Add the CloudWatch Logs metric filter + both alarms, apply, and manually trigger a test 5xx (a
   deliberately malformed authenticated request) to confirm the pipeline before trusting it.
3. Add the scheduled entry-point branch, the EventBridge rule, and its IAM permissions; apply;
   manually invoke once to confirm the digest email arrives with the expected content before
   trusting the daily schedule.
4. No rollback complexity — every piece here is additive infrastructure with no effect on existing
   request handling; removing any alarm/rule/subscription via terraform is a clean, isolated revert.
