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

# company-and-domain-launch: SesInvitationEmailSender sends org invitations through SES v2. Scoped
# to exactly the one domain identity in dns-and-email.tf — no other SES access, and none at all
# until domain_name is set. Sending is the only SES verb the running app needs; identity/DKIM
# management is the deploy role's job, not the function's.
data "aws_iam_policy_document" "hosted_api_ses" {
  count = local.domain_enabled ? 1 : 0
  statement {
    actions   = ["ses:SendEmail"]
    resources = [aws_ses_domain_identity.main[0].arn]
  }
}

resource "aws_iam_role_policy" "hosted_api_ses" {
  count  = local.domain_enabled ? 1 : 0
  name   = "ses-send-invitation-email"
  role   = aws_iam_role.hosted_api.id
  policy = data.aws_iam_policy_document.hosted_api_ses[0].json
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
      # plan-catalog-and-entitlements: Clerk user ids allowed to call the operator-only admin tier
      # endpoint (setting an org to Enterprise). Empty ⇒ the admin surface is closed.
      Admin__OperatorUserIds = var.admin_operator_user_ids

      # run-notifications: presence of the queue URL switches INotificationQueue from the no-op to the
      # SQS producer. Web__BaseUrl also builds the invite accept link + notification dashboard links.
      Notifications__QueueUrl = aws_sqs_queue.run_notifications.id
      Web__BaseUrl            = var.web_base_url

      # company-and-domain-launch: presence of this binds SesInvitationEmailSender (SES v2);
      # empty ⇒ LoggingInvitationEmailSender and the accept link is only in the API response.
      Notifications__FromAddress = var.notifications_from_address

      # onboarding-activation: the hosted API's own public URL, shown in the guided first-run panel's
      # CLI command. Can't reference aws_lambda_function_url here (circular) — supplied by
      # deploy-hosted.yml on the second apply, same two-pass pattern as the GitHub OAuth vars. Empty
      # ⇒ the panel shows a "https://YOUR-HOSTED-API" placeholder.
      Api__PublicUrl = var.api_public_url

      # billing: Polar (Merchant of Record). Empty ApiToken/WebhookSecret/product ids ⇒
      # PolarOptions.IsConfigured is false and every billing surface stays closed (the webhook
      # returns 503, the upgrade button errors gracefully). Secrets come from repo *secrets*,
      # identifiers from repo *variables* — see deploy-hosted.yml.
      Polar__ApiToken                  = var.polar_api_token
      Polar__WebhookSecret             = var.polar_webhook_secret
      Polar__ApiBaseUrl                = var.polar_api_base_url
      Polar__CheckoutSuccessUrl        = var.polar_checkout_success_url
      Polar__CheckoutCancelUrl         = var.polar_checkout_cancel_url
      Polar__PortalReturnUrl           = var.polar_portal_return_url
      Polar__ProductIds__Team__Monthly = var.polar_product_team_monthly
      Polar__ProductIds__Team__Annual  = var.polar_product_team_annual
      Polar__UpgradeEnabled            = tostring(var.polar_upgrade_enabled)
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

variable "admin_operator_user_ids" {
  description = "plan-catalog-and-entitlements: comma/space-separated Clerk user ids allowed to call the operator-only admin tier endpoint (PUT /api/admin/organizations/{id}/tier — the code path for granting Enterprise). Empty ⇒ nobody is an operator and the admin surface is closed. Supplied from the ADMIN_OPERATOR_USER_IDS repo variable by deploy-hosted.yml."
  type        = string
  default     = ""
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

variable "api_public_url" {
  description = "onboarding-activation: the hosted API's own public URL (the Lambda function URL), shown in the guided first-run panel's CLI command. Supplied on the second apply once the function URL is known — empty is safe (the panel shows a placeholder)."
  type        = string
  default     = ""
}

output "function_url" {
  value = aws_lambda_function_url.hosted_api.function_url
}
