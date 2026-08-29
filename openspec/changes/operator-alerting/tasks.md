## 1. Alerting channel

- [ ] 1.1 Add an SNS topic (`releasetwin-operator-alerts` or similar) to
      `hosted/terraform/` (new `alerting.tf`, or appended to `lambda.tf`).
- [ ] 1.2 Add an email subscription on that topic for the operator's own address.
- [ ] 1.3 Apply, then manually confirm the subscription from the operator's inbox — document this
      one-time manual step in the terraform file's own comments so it isn't missed on a future
      re-apply from scratch.

## 2. API health alarms

- [ ] 2.1 Confirm the exact format of the hosted API's existing request logs (status code field)
      to write an accurate CloudWatch Logs metric filter pattern against.
- [ ] 2.2 Add a CloudWatch Logs metric filter matching 5xx status codes in those logs, publishing a
      custom metric.
- [ ] 2.3 Add a CloudWatch Alarm on that metric (e.g. any 5xx in a 5-minute window), publishing to
      the SNS topic from task 1.
- [ ] 2.4 Add a CloudWatch Alarm on the Lambda's own `Errors`/`Throttles` metrics, publishing to
      the same SNS topic.
- [ ] 2.5 Apply, then trigger a deliberate 5xx (e.g. a malformed authenticated request) against the
      deployed API and confirm the alert email arrives.

## 3. Staleness digest

- [ ] 3.1 Add a way to list every project across every organization (a `ProjectRepository` method
      or a direct `IHostedTable` Scan filtered to `PROJECT#` items — see design.md's Decisions).
- [ ] 3.2 Add a scheduled entry-point branch to the hosted API's Lambda handler (detecting an
      EventBridge Scheduled Event vs. the existing HTTP API event source) that, for every project,
      gathers its upload timestamps and calls the existing `UploadStalenessCalculator.IsStale`.
- [ ] 3.3 If one or more projects are currently stale, publish a single digest message (listing
      each stale project's org and name) to the SNS topic from task 1; publish nothing when no
      project is stale.
- [ ] 3.4 Add the EventBridge rule (daily schedule) and its IAM permissions (invoke the Lambda,
      publish to the SNS topic) in terraform.
- [ ] 3.5 Apply, then manually invoke the scheduled path once (e.g. via `aws lambda invoke` with a
      synthetic EventBridge event, or by temporarily setting the schedule to a near-term one-off)
      and confirm the digest email arrives with the expected content before trusting the daily
      schedule.

## 4. Verification

- [ ] 4.1 Confirm existing hosted API request handling (the HTTP API event source path) is
      unaffected — run the existing Cypress suite against the local dev server as a regression
      check, since this change only adds a new branch to the Lambda entry point.
- [ ] 4.2 Document the new alerting behavior briefly in `docs/installation-model.md` or a new
      `docs/operator-alerting.md` — what triggers each alarm/digest, and where the email goes —
      since this is operator-facing infrastructure with no UI of its own to make it discoverable.
