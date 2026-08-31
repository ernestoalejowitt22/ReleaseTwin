# billing (openspec/changes/billing-integration): Polar Merchant-of-Record configuration + the
# nightly subscription-quantity reconciliation Lambda.
#
# Everything defaults to empty / dry-run so this file can be applied before Polar exists at all —
# PolarOptions.IsConfigured is false until ApiToken + WebhookSecret + a product id are all set, and
# with it false the webhook returns 503 and the dashboard upgrade button degrades gracefully (the
# "billing surface closed" safe default, tasks.md 1.2). The webhook endpoint itself lives in the
# main hosted_api function (lambda.tf) — its DynamoDB policy already covers the GetItem/PutItem the
# ProcessedBillingEvent idempotency item needs, and the table's TTL is enabled in main.tf.

variable "polar_api_token" {
  description = "billing: Polar organization access token (POLAR_API_TOKEN secret). Empty ⇒ billing disabled."
  type        = string
  default     = ""
  sensitive   = true
}

variable "polar_webhook_secret" {
  description = "billing: Polar webhook signing secret (POLAR_WEBHOOK_SECRET secret), Standard Webhooks scheme. Empty ⇒ the webhook rejects every delivery."
  type        = string
  default     = ""
  sensitive   = true
}

variable "polar_api_base_url" {
  description = "billing: Polar REST base URL. Use https://sandbox-api.polar.sh for the sandbox."
  type        = string
  default     = "https://api.polar.sh"
}

variable "polar_product_team_monthly" {
  description = "billing: Polar product id for the Team / monthly-cadence product (POLAR_TEAM_PRODUCT_MONTHLY variable). The checkout API takes product ids."
  type        = string
  default     = ""
}

variable "polar_product_team_annual" {
  description = "billing: Polar product id for the Team / annual-cadence product (POLAR_TEAM_PRODUCT_ANNUAL variable)."
  type        = string
  default     = ""
}

variable "polar_checkout_success_url" {
  description = "billing: where Polar returns the buyer after a completed checkout (a dashboard URL)."
  type        = string
  default     = ""
}

variable "polar_checkout_cancel_url" {
  description = "billing: where Polar returns the buyer after an abandoned checkout."
  type        = string
  default     = ""
}

variable "polar_portal_return_url" {
  description = "billing: where Polar returns the customer after they close the hosted portal."
  type        = string
  default     = ""
}

variable "polar_reconciliation_dry_run" {
  description = "billing (design.md Migration Plan): the reconciliation job logs intended corrections and calls nothing while true. Flip to false after one clean nightly cycle."
  type        = bool
  default     = true
}

variable "polar_upgrade_enabled" {
  description = "billing (design.md Migration Plan step 3 vs 5): the webhook goes live as soon as the Polar secrets + product ids are set, but the customer-facing dashboard upgrade / portal buttons stay closed until this is flipped true — after a real sandbox checkout has been verified end to end."
  type        = bool
  default     = false
}

# --- Reconciliation Lambda: a scheduled deployable sharing the HTTP function's artifact -------------
#
# Same pattern as alerting.tf's staleness digest and evidence.tf's purge job: a distinct
# aws_lambda_function pointing at the same package, discriminated by the static
# RELEASETWIN_LAMBDA_TASK env var. Read-only on DynamoDB (Scan every org, Query each org's projects);
# all mutations it makes are HTTP calls to Polar, which need no IAM.
data "aws_iam_policy_document" "billing_reconciliation_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "billing_reconciliation" {
  name               = "${var.table_prefix}releasetwin-billing-reconciliation-lambda"
  assume_role_policy = data.aws_iam_policy_document.billing_reconciliation_assume_role.json
}

resource "aws_iam_role_policy_attachment" "billing_reconciliation_logs" {
  role       = aws_iam_role.billing_reconciliation.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

data "aws_iam_policy_document" "billing_reconciliation_dynamodb" {
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

resource "aws_iam_role_policy" "billing_reconciliation_dynamodb" {
  name   = "dynamodb-read-access"
  role   = aws_iam_role.billing_reconciliation.id
  policy = data.aws_iam_policy_document.billing_reconciliation_dynamodb.json
}

resource "aws_lambda_function" "billing_reconciliation" {
  function_name = "${var.table_prefix}releasetwin-billing-reconciliation"
  role          = aws_iam_role.billing_reconciliation.arn

  handler = "ReleaseTwin.Hosted.Api"
  runtime = "dotnet10"

  # Full-table Scan plus one Polar round-trip per linked org — same slow-path reasoning as the other
  # scheduled jobs' 60s.
  timeout     = 60
  memory_size = 512

  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)

  environment {
    variables = {
      RELEASETWIN_LAMBDA_TASK          = "BillingReconciliation"
      Aws__Region                      = var.region
      Aws__DynamoDb__TablePrefix       = var.table_prefix
      Polar__ApiToken                  = var.polar_api_token
      Polar__WebhookSecret             = var.polar_webhook_secret
      Polar__ApiBaseUrl                = var.polar_api_base_url
      Polar__ProductIds__Team__Monthly = var.polar_product_team_monthly
      Polar__ProductIds__Team__Annual  = var.polar_product_team_annual
      Polar__ReconciliationDryRun      = tostring(var.polar_reconciliation_dry_run)
    }
  }
}

resource "aws_cloudwatch_event_rule" "billing_reconciliation_schedule" {
  name                = "${var.table_prefix}releasetwin-billing-reconciliation-daily"
  description         = "Runs the Polar subscription-quantity reconciliation once a day (design.md D6 backstop)."
  schedule_expression = "rate(1 day)"
}

resource "aws_cloudwatch_event_target" "billing_reconciliation" {
  rule = aws_cloudwatch_event_rule.billing_reconciliation_schedule.name
  arn  = aws_lambda_function.billing_reconciliation.arn
}

resource "aws_lambda_permission" "allow_eventbridge_billing_reconciliation" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.billing_reconciliation.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.billing_reconciliation_schedule.arn
}
