## 1. Alerting channel

- [x] 1.1 SNS topic added in `hosted/terraform/alerting.tf` (`aws_sns_topic.operator_alerts`,
      named `${var.table_prefix}releasetwin-operator-alerts`).
- [x] 1.2 Email subscription added (`aws_sns_topic_subscription.operator_alert_email`), gated on
      the new `operator_alert_email` terraform variable being non-empty — left empty, no
      subscription is created and the digest logs a warning instead of failing.
- [ ] 1.3 **Needs the user to run this** — I have no AWS write access in this environment (my
      configured identity, `releasetwin-e2e-secrets-reader`, is read-only and scoped to Secrets
      Manager; deploys already go exclusively through the `Deploy Hosted API` GitHub Actions
      workflow via OIDC, matching `hosted-platform-deployment`'s existing design, not a local
      `terraform apply`). Before this can go live: set the `OPERATOR_ALERT_EMAIL` repo variable,
      run the workflow, then confirm the SNS subscription from that inbox. Documented in
      `docs/operator-alerting.md` and in `alerting.tf`'s own comments.

## 2. API health alarms

- [x] 2.1 Confirmed: no request-level log line existed at all — both `appsettings.json` and
      `appsettings.Development.json` set `Microsoft.AspNetCore` logging to `Warning`, suppressing
      the framework's own built-in request-finished log. Fixed by adding a deliberate, stable-format
      request-completion log line in `Program.cs` (`http_request_completed status=... method=...
      path=...`) rather than relying on the framework's own, format-unstable one — see design.md's
      Context addendum.
- [x] 2.2 CloudWatch Logs metric filter added (`aws_cloudwatch_log_metric_filter.hosted_api_5xx` in
      `alerting.tf`), matching `"http_request_completed status=5"` against the hosted API's
      existing (auto-created) log group.
- [x] 2.3 CloudWatch Alarm added on that metric (`aws_cloudwatch_metric_alarm.hosted_api_5xx`), any
      5xx in a 5-minute window, publishing to the SNS topic.
- [x] 2.4 CloudWatch Alarms added on the Lambda's own `Errors` and `Throttles` metrics
      (`hosted_api_lambda_errors`, `hosted_api_lambda_throttles`), publishing to the same topic.
- [ ] 2.5 **Needs the user to run this** — same reason as 1.3. After a real deploy: trigger a
      deliberate 5xx against the deployed Function URL and confirm the alert email arrives.

## 3. Staleness digest

- [x] 3.1 `IHostedTable.ScanByEntityTypeAsync` (both `DynamoDbHostedTable` and
      `InMemoryHostedTable`) plus `IProjectRepository.ListAllAsync` added — a full-table Scan
      filtered to `EntityType = "Project"`, the one caller with no per-org partition key to scope a
      Query to. Covered by `StalenessDigestServiceTests.DigestCoversProjectsAcrossEveryOrganization`.
- [x] 3.2 Real design change from the original plan (see design.md's Decisions — updated in place
      with the reasoning): `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` can't route an
      EventBridge Scheduled Event through the same pipeline an HTTP request uses, and Lambda env
      vars are static per function, not per invocation, so a single function can't branch on them
      per-request either. Implemented instead as a second `aws_lambda_function` resource sharing
      the same build artifact, discriminated by the `RELEASETWIN_LAMBDA_TASK` environment variable;
      `Program.cs` runs its own `LambdaBootstrapBuilder` loop in that mode instead of the ASP.NET
      Core pipeline. `StalenessDigestService` (new) gathers every project's upload timestamps and
      calls the existing `UploadStalenessCalculator.IsStale`, unchanged.
- [x] 3.3 Digest publish implemented via a new `IOperatorAlertPublisher`/`SnsOperatorAlertPublisher`
      (kept narrow and separate from the full AWS SNS SDK interface specifically so it's unit
      testable — see `StalenessDigestServiceTests`, using an in-memory fake). Publishes one digest
      message listing every currently-stale project's org/name/last-upload; publishes nothing when
      none are stale (`StalenessDigestServiceTests.NothingStalePublishesNoDigest`).
- [x] 3.4 EventBridge rule (`aws_cloudwatch_event_rule.staleness_digest_schedule`, `rate(1 day)`),
      its target, and the `lambda:InvokeFunction` permission for `events.amazonaws.com` all added in
      `alerting.tf`, along with a dedicated least-privilege IAM role (DynamoDB Query+Scan read-only,
      `sns:Publish` on just this topic — no write access to the table at all).
- [ ] 3.5 **Needs the user to run this** — same reason as 1.3/2.5. After a real deploy: manually
      invoke the `<prefix>releasetwin-staleness-digest` Lambda once and confirm the digest email's
      content before trusting the daily schedule.

## 4. Verification

- [x] 4.1 `dotnet test` (hosted API, all 87 pre-existing + 4 new tests) passes. Ran
      `dashboard-walkthrough.cy.ts` against the local dev server (the actual HTTP request path,
      unaffected by the digest-mode branch since `RELEASETWIN_LAMBDA_TASK` is unset locally) to
      confirm the new request-logging middleware and DI registrations don't break real request
      handling — see below for the result of this run.
- [x] 4.2 Documented in `docs/operator-alerting.md` — what triggers each alarm/digest, the
      one-time setup steps after a deploy, and a pointer back to design.md for the full rationale.
