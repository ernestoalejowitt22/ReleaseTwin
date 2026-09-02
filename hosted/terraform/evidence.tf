# evidence-purge-and-blob-store: makes dashboard-evidence-viewer's already-specified retention
# behavior actually run in production — a private S3 bucket for redacted screenshot blobs (the
# filesystem store can't survive Lambda's ephemeral fs), and a second scheduled Lambda that runs
# EvidencePurgeService once a day. Same shape as alerting.tf's staleness digest, with one
# difference: this job *deletes* rows, so its DynamoDB policy is read-write, not read-only.

# --- Blob bucket -----------------------------------------------------------------------------------
#
# No lifecycle rule: the app owns deletion timing (each project's own retention window, and
# "lowering the window makes old evidence eligible immediately" — evidence-store spec). A fixed-age
# lifecycle rule would delete on a different clock and break that contract. SSE-S3 (not KMS): the
# objects are already CLI-redacted screenshots, and AES256 is free.
resource "aws_s3_bucket" "evidence_blobs" {
  bucket = "${var.table_prefix}releasetwin-evidence-blobs"
}

resource "aws_s3_bucket_public_access_block" "evidence_blobs" {
  bucket                  = aws_s3_bucket.evidence_blobs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "evidence_blobs" {
  bucket = aws_s3_bucket.evidence_blobs.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# security-hardening-pre-pilot D3: defence-in-depth for the "one project can't overwrite another
# project's screenshot" guarantee. Blob keys are now project-namespaced
# (screenshots/<projectId>/<id>), so a collision needs a bug rather than a hostile id — versioning
# makes such an overwrite recoverable instead of silent-and-final. The exports/ lifecycle rule below
# still expires those objects (noncurrent versions included, added there).
resource "aws_s3_bucket_versioning" "evidence_blobs" {
  bucket = aws_s3_bucket.evidence_blobs.id

  versioning_configuration {
    status = "Enabled"
  }
}

# data-export: built export archives are PUT under exports/<orgId>/... and downloaded once via a
# 1-hour presigned URL — they are transient, not storage. This rule expires ONLY that prefix; the
# screenshot blobs (under screenshots/<projectId>/, security-hardening-pre-pilot D3) are untouched by
# the age rule, so the "the app owns deletion timing" contract above still holds.
resource "aws_s3_bucket_lifecycle_configuration" "evidence_blobs_exports" {
  bucket = aws_s3_bucket.evidence_blobs.id

  rule {
    id     = "expire-data-exports"
    status = "Enabled"

    filter {
      prefix = "exports/"
    }

    expiration {
      days = 7
    }

    # Versioning (security-hardening-pre-pilot D3) is bucket-wide — keep old export versions from
    # piling up under the same 7-day clock.
    noncurrent_version_expiration {
      noncurrent_days = 7
    }
  }

  # Screenshot blobs: bounded cleanup of superseded versions (an overwrite should be rare; keep a
  # short recovery window, not forever).
  rule {
    id     = "expire-noncurrent-screenshot-versions"
    status = "Enabled"

    filter {
      prefix = "screenshots/"
    }

    noncurrent_version_expiration {
      noncurrent_days = 30
    }
  }
}

# --- Purge Lambda: a second deployable sharing the HTTP function's artifact --------------------------
#
# alerting.tf's staleness digest explains the pattern: AddAWSLambdaHosting marshals every invocation
# as an API Gateway HTTP API v2 request, which an EventBridge Scheduled Event is not — so rather than
# teach one function to dispatch two event shapes, this is a distinct aws_lambda_function pointing at
# the same package, discriminated by the static RELEASETWIN_LAMBDA_TASK env var.
data "aws_iam_policy_document" "evidence_purge_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "evidence_purge" {
  name               = "${var.table_prefix}releasetwin-evidence-purge-lambda"
  assume_role_policy = data.aws_iam_policy_document.evidence_purge_assume_role.json
}

resource "aws_iam_role_policy_attachment" "evidence_purge_logs" {
  role       = aws_iam_role.evidence_purge.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# Read-write on the table (unlike the read-only staleness digest): Scan to find every project's
# expired evidence across all organizations, Query to read a project's retention window, DeleteItem
# to remove an expired evidence row. Never touches the metadata report rows.
data "aws_iam_policy_document" "evidence_purge_dynamodb" {
  statement {
    actions = [
      "dynamodb:Query",
      "dynamodb:Scan",
      "dynamodb:DeleteItem",
    ]
    resources = [
      aws_dynamodb_table.hosted.arn,
      "${aws_dynamodb_table.hosted.arn}/index/*",
    ]
  }
}

resource "aws_iam_role_policy" "evidence_purge_dynamodb" {
  name   = "dynamodb-purge-access"
  role   = aws_iam_role.evidence_purge.id
  policy = data.aws_iam_policy_document.evidence_purge_dynamodb.json
}

data "aws_iam_policy_document" "evidence_purge_s3" {
  statement {
    actions   = ["s3:DeleteObject"]
    resources = ["${aws_s3_bucket.evidence_blobs.arn}/*"]
  }

  statement {
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.evidence_blobs.arn]
  }
}

resource "aws_iam_role_policy" "evidence_purge_s3" {
  name   = "s3-purge-access"
  role   = aws_iam_role.evidence_purge.id
  policy = data.aws_iam_policy_document.evidence_purge_s3.json
}

resource "aws_lambda_function" "evidence_purge" {
  function_name = "${var.table_prefix}releasetwin-evidence-purge"
  role          = aws_iam_role.evidence_purge.arn

  handler = "ReleaseTwin.Hosted.Api"
  runtime = "dotnet10"

  # A full-table Scan plus N blob deletes — same slow-path reasoning as the staleness digest's 60s.
  timeout     = 60
  memory_size = 512

  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)

  environment {
    variables = {
      RELEASETWIN_LAMBDA_TASK    = "EvidencePurge"
      Aws__Region                = var.region
      Aws__DynamoDb__TablePrefix = var.table_prefix
      Evidence__BlobBucket       = aws_s3_bucket.evidence_blobs.id
    }
  }
}

resource "aws_cloudwatch_event_rule" "evidence_purge_schedule" {
  name                = "${var.table_prefix}releasetwin-evidence-purge-daily"
  description         = "Runs the evidence purge once a day (evidence-store: 'recurring purge')."
  schedule_expression = "rate(1 day)"
}

resource "aws_cloudwatch_event_target" "evidence_purge" {
  rule = aws_cloudwatch_event_rule.evidence_purge_schedule.name
  arn  = aws_lambda_function.evidence_purge.arn
}

resource "aws_lambda_permission" "allow_eventbridge_evidence_purge" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.evidence_purge.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.evidence_purge_schedule.arn
}

output "evidence_blob_bucket" {
  value = aws_s3_bucket.evidence_blobs.id
}
