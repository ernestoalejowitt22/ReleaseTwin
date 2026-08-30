# hosted-platform-deployment design.md: Lambda hosting for ReleaseTwin.Hosted.Api, in the same
# terraform apply as the DynamoDB table in main.tf rather than a separate hand-run
# `dotnet lambda deploy-function` — one coherent, plannable AWS footprint.
#
# Terraform doesn't compile .NET code: build the deployment zip first with
#   dotnet lambda package --output-package hosted/terraform/lambda-package.zip
# from hosted/ReleaseTwin.Hosted.Api, then `terraform apply` (this file's `lambda_package_path`
# variable defaults to that same path).
#
# Two-pass apply: the GitHubConnection__* variables default to empty strings so this can be
# applied before the GitHub OAuth App (which needs the Vercel URL, which needs this function's
# URL) exists at all — the app already handles that config being absent gracefully
# (ConnectionEndpoints.cs). Re-apply with the real values once known (see tasks.md group 4).

data "aws_iam_policy_document" "hosted_api_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "hosted_api" {
  name               = "${var.table_prefix}releasetwin-hosted-api-lambda"
  assume_role_policy = data.aws_iam_policy_document.hosted_api_assume_role.json
}

# CloudWatch Logs only — the function can't log at all without this.
resource "aws_iam_role_policy_attachment" "hosted_api_logs" {
  role       = aws_iam_role.hosted_api.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# design.md: least-privilege, not admin — scoped to exactly the one table and its two GSIs, nothing
# beyond DynamoDB.
data "aws_iam_policy_document" "hosted_api_dynamodb" {
  statement {
    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:Query",
    ]
    resources = [
      aws_dynamodb_table.hosted.arn,
      "${aws_dynamodb_table.hosted.arn}/index/*",
    ]
  }
}

resource "aws_iam_role_policy" "hosted_api_dynamodb" {
  name   = "dynamodb-access"
  role   = aws_iam_role.hosted_api.id
  policy = data.aws_iam_policy_document.hosted_api_dynamodb.json
}

# evidence-purge-and-blob-store: the API writes a redacted screenshot blob on ingest (and could
# delete one from a future "delete this evidence now" endpoint). Reads go through the same store on
# the dashboard's BFF screenshot proxy. Bucket defined in evidence.tf.
data "aws_iam_policy_document" "hosted_api_evidence_s3" {
  statement {
    actions = [
      "s3:PutObject",
      "s3:GetObject",
      "s3:DeleteObject",
    ]
    resources = ["${aws_s3_bucket.evidence_blobs.arn}/*"]
  }
}

resource "aws_iam_role_policy" "hosted_api_evidence_s3" {
  name   = "evidence-blob-s3-access"
  role   = aws_iam_role.hosted_api.id
  policy = data.aws_iam_policy_document.hosted_api_evidence_s3.json
}

# hosted-adapter-credentials design.md Migration Plan: the real-AWS path persists the Data
# Protection key ring (used by ConnectionStateService and AdapterCredentialService) to SSM Parameter
# Store as SecureString (PersistKeysToAWSSystemsManager) — without this, ANY code path that creates
# a data protector (including the pre-existing GitHub connection flow) fails, not just
# adapter-credentials specifically. Scoped to exactly the one parameter path this table's key ring
# uses, plus KMS access to the default `alias/aws/ssm` key SecureString encryption uses.
data "aws_iam_policy_document" "hosted_api_data_protection" {
  statement {
    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParametersByPath",
      "ssm:PutParameter",
    ]
    resources = [
      "arn:aws:ssm:${var.region}:*:parameter/${var.table_prefix}ReleaseTwinHosted/DataProtection/Keys/*",
    ]
  }

  statement {
    actions = [
      "kms:Decrypt",
      "kms:Encrypt",
      "kms:GenerateDataKey",
    ]
    resources = [
      "arn:aws:kms:${var.region}:*:alias/aws/ssm",
    ]
  }
}

resource "aws_iam_role_policy" "hosted_api_data_protection" {
  name   = "data-protection-ssm-access"
  role   = aws_iam_role.hosted_api.id
  policy = data.aws_iam_policy_document.hosted_api_data_protection.json
}

resource "aws_lambda_function" "hosted_api" {
  function_name = "${var.table_prefix}releasetwin-hosted-api"
  role          = aws_iam_role.hosted_api.arn

  # design.md: Amazon.Lambda.AspNetCoreServer.Hosting's AddAWSLambdaHosting needs no custom
  # LambdaEntryPoint class — the handler is just the assembly name (no .dll extension).
  handler = "ReleaseTwin.Hosted.Api"
  runtime = "dotnet10"

  timeout     = 30
  memory_size = 512

  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)

  environment {
    variables = {
      Clerk__Domain                  = var.clerk_domain
      Aws__Region                    = var.region
      Aws__DynamoDb__TablePrefix     = var.table_prefix
      GitHubConnection__ClientId     = var.github_client_id
      GitHubConnection__ClientSecret = var.github_client_secret
      GitHubConnection__CallbackUrl  = var.github_callback_url
      # evidence-purge-and-blob-store: presence of this switches the blob store from filesystem to S3.
      Evidence__BlobBucket = aws_s3_bucket.evidence_blobs.id
    }
  }
}

# design.md: Function URL, not API Gateway — no domain/ACM/ALB needed. AuthType NONE: the app
# already has its own two auth schemes (ClerkJwt, ApiToken); a second IAM-based layer in front
# would be redundant, not additive security.
resource "aws_lambda_function_url" "hosted_api" {
  function_name      = aws_lambda_function.hosted_api.function_name
  authorization_type = "NONE"
}

variable "lambda_package_path" {
  description = "Path to the zip produced by `dotnet lambda package` (see tasks.md 1.4)."
  type        = string
  default     = "./lambda-package.zip"
}

variable "clerk_domain" {
  description = "Clerk Frontend API domain (e.g. <slug>.clerk.accounts.dev for a dev instance, or a custom domain for production) used for JWT issuer validation. Supplied per environment — `deploy-hosted.yml` passes it from the CLERK_DOMAIN repo variable. No default: a wrong or stale issuer silently rejects every token."
  type        = string

  validation {
    condition     = length(var.clerk_domain) > 0
    error_message = "clerk_domain must be set (CLERK_DOMAIN repo variable / -var). It is the JWT issuer the API validates against."
  }
}

variable "github_client_id" {
  description = "GitHub OAuth App Client ID (project-connections). Empty until the app is registered against the deployed Vercel URL — see tasks.md group 4."
  type        = string
  default     = ""
}

variable "github_client_secret" {
  description = "GitHub OAuth App Client Secret."
  type        = string
  default     = ""
  sensitive   = true
}

variable "github_callback_url" {
  description = "GitHub OAuth App callback URL, pointed at the deployed Vercel URL once known."
  type        = string
  default     = ""
}

output "function_url" {
  value = aws_lambda_function_url.hosted_api.function_url
}
