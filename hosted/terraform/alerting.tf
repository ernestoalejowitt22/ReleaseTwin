# operator-alerting: two things the operator previously had zero signal on — the hosted API
# returning errors, and a customer's project going quiet — both landing in the same inbox via one
# SNS topic. See openspec/changes/operator-alerting/design.md for the full rationale.

variable "operator_alert_email" {
  description = "Email address subscribed to the operator alerts SNS topic. Left empty, no subscription is created (and the digest Lambda logs a warning and skips publishing instead of failing) — set this on a real deploy, see tasks.md group 1."
  type        = string
  default     = ""
}

resource "aws_sns_topic" "operator_alerts" {
  name = "${var.table_prefix}releasetwin-operator-alerts"
}

# SNS can't auto-confirm an email subscription (by design, to prevent spam) — after `terraform
# apply`, the operator must click the confirmation link AWS emails to operator_alert_email once.
# Nothing in this file can automate that step; documented again in docs/operator-alerting.md.
resource "aws_sns_topic_subscription" "operator_alert_email" {
  count     = var.operator_alert_email != "" ? 1 : 0
  topic_arn = aws_sns_topic.operator_alerts.arn
  protocol  = "email"
  endpoint  = var.operator_alert_email
}

# --- API health: 5xx rate ---------------------------------------------------------------------
#
# design.md: Lambda's own "Errors" metric only fires on an unhandled exception escaping the
# function entirely — this project's UseExceptionHandler catches nearly everything and returns a
# clean HTTP 500, which Lambda counts as a *successful* invocation. The only honest signal is the
# request-completion log line Program.cs now emits on every request (see its own comment) via a
# CloudWatch Logs metric filter. Deliberately not an explicit aws_cloudwatch_log_group resource
# here — the hosted_api function has already been running in production (hosted-platform-
# deployment) and therefore already has an auto-created log group by this exact name; declaring
# one in terraform would fail `apply` with "already exists" unless imported first. A metric filter
# only needs the log group's name as a string, not ownership of the resource.
resource "aws_cloudwatch_log_metric_filter" "hosted_api_5xx" {
  name           = "${var.table_prefix}releasetwin-hosted-api-5xx"
  log_group_name = "/aws/lambda/${aws_lambda_function.hosted_api.function_name}"
  pattern        = "\"http_request_completed status=5\""

  metric_transformation {
    name          = "HostedApi5xxCount"
    namespace     = "ReleaseTwin/HostedApi"
    value         = "1"
    default_value = "0"
  }
}

resource "aws_cloudwatch_metric_alarm" "hosted_api_5xx" {
  alarm_name          = "${var.table_prefix}releasetwin-hosted-api-5xx"
  alarm_description   = "The hosted API returned one or more 5xx responses in the last 5 minutes."
  namespace           = aws_cloudwatch_log_metric_filter.hosted_api_5xx.metric_transformation[0].namespace
  metric_name         = aws_cloudwatch_log_metric_filter.hosted_api_5xx.metric_transformation[0].name
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  comparison_operator = "GreaterThanThreshold"
  threshold           = 0
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.operator_alerts.arn]
  ok_actions          = [aws_sns_topic.operator_alerts.arn]
}

# --- API health: escaped exceptions / throttles ------------------------------------------------
#
# design.md: catches exactly the failure class the log-based filter above can't see — a crash
# before the app ever produces a response, a timeout, an out-of-memory kill, or Lambda itself
# throttling the function under load. Free — no extra logging needed, Lambda emits these natively.
resource "aws_cloudwatch_metric_alarm" "hosted_api_lambda_errors" {
  alarm_name          = "${var.table_prefix}releasetwin-hosted-api-lambda-errors"
  alarm_description   = "The hosted API Lambda itself threw an unhandled exception (crash, timeout, OOM)."
  namespace           = "AWS/Lambda"
  metric_name         = "Errors"
  dimensions          = { FunctionName = aws_lambda_function.hosted_api.function_name }
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  comparison_operator = "GreaterThanThreshold"
  threshold           = 0
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.operator_alerts.arn]
  ok_actions          = [aws_sns_topic.operator_alerts.arn]
}

resource "aws_cloudwatch_metric_alarm" "hosted_api_lambda_throttles" {
  alarm_name          = "${var.table_prefix}releasetwin-hosted-api-lambda-throttles"
  alarm_description   = "The hosted API Lambda is being throttled by AWS Lambda concurrency limits."
  namespace           = "AWS/Lambda"
  metric_name         = "Throttles"
  dimensions          = { FunctionName = aws_lambda_function.hosted_api.function_name }
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  comparison_operator = "GreaterThanThreshold"
  threshold           = 0
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.operator_alerts.arn]
  ok_actions          = [aws_sns_topic.operator_alerts.arn]
}

# --- Staleness digest: a second, scheduled Lambda sharing the HTTP function's deployment artifact ---
#
# design.md: AddAWSLambdaHosting(LambdaEventSource.HttpApi) marshals every invocation as an API
# Gateway HTTP API v2 proxy request — an EventBridge Scheduled Event has a completely different
# shape and can't be routed through that same pipeline. Rather than teach one Lambda function to
# sniff and dispatch between two incompatible event shapes, this declares a second `aws_lambda_
# function` resource pointing at the exact same package (one build, two deployables) — Program.cs
# branches on the RELEASETWIN_LAMBDA_TASK environment variable, which is static per function and
# therefore a clean discriminator between the two.
data "aws_iam_policy_document" "staleness_digest_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "staleness_digest" {
  name               = "${var.table_prefix}releasetwin-staleness-digest-lambda"
  assume_role_policy = data.aws_iam_policy_document.staleness_digest_assume_role.json
}

resource "aws_iam_role_policy_attachment" "staleness_digest_logs" {
  role       = aws_iam_role.staleness_digest.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# Least-privilege, read-only: this job never writes to the table, and needs Scan (to cross every
# organization's partition — see IHostedTable.ScanByEntityTypeAsync) in addition to the Query every
# other read here already uses.
data "aws_iam_policy_document" "staleness_digest_dynamodb" {
  statement {
    actions = [
      "dynamodb:Query",
      "dynamodb:Scan",
    ]
    resources = [
      aws_dynamodb_table.hosted.arn,
      "${aws_dynamodb_table.hosted.arn}/index/*",
    ]
  }
}

resource "aws_iam_role_policy" "staleness_digest_dynamodb" {
  name   = "dynamodb-read-access"
  role   = aws_iam_role.staleness_digest.id
  policy = data.aws_iam_policy_document.staleness_digest_dynamodb.json
}

data "aws_iam_policy_document" "staleness_digest_sns" {
  statement {
    actions   = ["sns:Publish"]
    resources = [aws_sns_topic.operator_alerts.arn]
  }
}

resource "aws_iam_role_policy" "staleness_digest_sns" {
  name   = "sns-publish-access"
  role   = aws_iam_role.staleness_digest.id
  policy = data.aws_iam_policy_document.staleness_digest_sns.json
}

resource "aws_lambda_function" "staleness_digest" {
  function_name = "${var.table_prefix}releasetwin-staleness-digest"
  role          = aws_iam_role.staleness_digest.arn

  handler = "ReleaseTwin.Hosted.Api"
  runtime = "dotnet10"

  # Scanning the whole table is the slow part (design.md accepts this at current scale) — a longer
  # timeout than the HTTP function's 30s, still well within a single EventBridge invocation.
  timeout     = 60
  memory_size = 512

  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)

  environment {
    variables = {
      RELEASETWIN_LAMBDA_TASK    = "StalenessDigest"
      Aws__Region                = var.region
      Aws__DynamoDb__TablePrefix = var.table_prefix
      Alerting__OperatorTopicArn = aws_sns_topic.operator_alerts.arn
    }
  }
}

resource "aws_cloudwatch_event_rule" "staleness_digest_schedule" {
  name                = "${var.table_prefix}releasetwin-staleness-digest-daily"
  description         = "Runs the staleness digest once a day (design.md: daily cadence, not per-project alerting)."
  schedule_expression = "rate(1 day)"
}

resource "aws_cloudwatch_event_target" "staleness_digest" {
  rule = aws_cloudwatch_event_rule.staleness_digest_schedule.name
  arn  = aws_lambda_function.staleness_digest.arn
}

resource "aws_lambda_permission" "allow_eventbridge_staleness_digest" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.staleness_digest.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.staleness_digest_schedule.arn
}

output "operator_alerts_topic_arn" {
  value = aws_sns_topic.operator_alerts.arn
}
