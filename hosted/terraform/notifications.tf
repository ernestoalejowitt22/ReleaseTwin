# run-notifications (commercial-readiness-gaps design D6): outbound run-failure notifications are
# delivered off the ingest path. The hosted API enqueues a RunNotification onto this SQS queue; a
# second Lambda function (same deployment artifact, RELEASETWIN_LAMBDA_TASK=NotificationDispatch)
# drains it, POSTs to each project's enabled targets, and records the outcome. Same shared-artifact
# pattern as the scheduled Lambdas in alerting.tf / evidence.tf — but SQS-triggered, not scheduled.

variable "web_base_url" {
  description = "Public base URL of the marketing/dashboard site (e.g. https://app.releasetwin.com). Used to build the dashboard deep-link in a notification payload. Empty ⇒ the payload carries a relative /dashboard path."
  type        = string
  default     = ""
}

resource "aws_sqs_queue" "run_notifications_dlq" {
  name                      = "${var.table_prefix}releasetwin-run-notifications-dlq"
  message_retention_seconds = 1209600 # 14 days — long enough to inspect poison messages
}

resource "aws_sqs_queue" "run_notifications" {
  name                       = "${var.table_prefix}releasetwin-run-notifications"
  visibility_timeout_seconds = 60 # >= the dispatcher Lambda timeout below
  message_retention_seconds  = 345600

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.run_notifications_dlq.arn
    maxReceiveCount     = 5
  })
}

# --- Producer: let the hosted API send to the queue -------------------------------------------
data "aws_iam_policy_document" "hosted_api_sqs_send" {
  statement {
    actions   = ["sqs:SendMessage", "sqs:GetQueueAttributes"]
    resources = [aws_sqs_queue.run_notifications.arn]
  }
}

resource "aws_iam_role_policy" "hosted_api_sqs_send" {
  name   = "run-notifications-sqs-send"
  role   = aws_iam_role.hosted_api.id
  policy = data.aws_iam_policy_document.hosted_api_sqs_send.json
}

# --- Consumer: the notification dispatcher Lambda ---------------------------------------------
data "aws_iam_policy_document" "notification_dispatcher_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "notification_dispatcher" {
  name               = "${var.table_prefix}releasetwin-notification-dispatcher-lambda"
  assume_role_policy = data.aws_iam_policy_document.notification_dispatcher_assume_role.json
}

resource "aws_iam_role_policy_attachment" "notification_dispatcher_logs" {
  role       = aws_iam_role.notification_dispatcher.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

data "aws_iam_policy_document" "notification_dispatcher_sqs" {
  statement {
    actions = [
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:GetQueueAttributes",
    ]
    resources = [aws_sqs_queue.run_notifications.arn]
  }
}

resource "aws_iam_role_policy" "notification_dispatcher_sqs" {
  name   = "run-notifications-sqs-consume"
  role   = aws_iam_role.notification_dispatcher.id
  policy = data.aws_iam_policy_document.notification_dispatcher_sqs.json
}

# Least-privilege: read org / project / targets, write only the per-target LastOutcome back.
data "aws_iam_policy_document" "notification_dispatcher_dynamodb" {
  statement {
    actions = [
      "dynamodb:GetItem",
      "dynamodb:Query",
      "dynamodb:PutItem",
    ]
    resources = [
      aws_dynamodb_table.hosted.arn,
      "${aws_dynamodb_table.hosted.arn}/index/*",
    ]
  }
}

resource "aws_iam_role_policy" "notification_dispatcher_dynamodb" {
  name   = "dynamodb-access"
  role   = aws_iam_role.notification_dispatcher.id
  policy = data.aws_iam_policy_document.notification_dispatcher_dynamodb.json
}

resource "aws_lambda_function" "notification_dispatcher" {
  function_name = "${var.table_prefix}releasetwin-notification-dispatcher"
  role          = aws_iam_role.notification_dispatcher.arn

  handler = "ReleaseTwin.Hosted.Api"
  runtime = "dotnet10"

  timeout     = 30
  memory_size = 512

  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)

  environment {
    variables = {
      RELEASETWIN_LAMBDA_TASK    = "NotificationDispatch"
      Aws__Region                = var.region
      Aws__DynamoDb__TablePrefix = var.table_prefix
      Web__BaseUrl               = var.web_base_url
    }
  }
}

resource "aws_lambda_event_source_mapping" "notification_dispatcher" {
  event_source_arn                   = aws_sqs_queue.run_notifications.arn
  function_name                      = aws_lambda_function.notification_dispatcher.arn
  batch_size                         = 10
  maximum_batching_window_in_seconds = 5
  function_response_types            = ["ReportBatchItemFailures"]
}

output "run_notifications_queue_url" {
  value = aws_sqs_queue.run_notifications.id
}
