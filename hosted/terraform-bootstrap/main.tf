# hosted-platform-deployment design.md: the GitHub Actions OIDC trust and the CI role that
# hosted/terraform's deploy workflow assumes — everything downstream of hosted/terraform-state-
# backend (whose bucket/table this config uses as its own remote backend, so unlike that layer,
# this one's state is durable from its very first apply).
#
# Run via the "oidc-and-role" job in .github/workflows/bootstrap.yml, authenticated the same way
# as terraform-state-backend (a short-lived MFA session pasted into repo secrets for that one run)
# — this config creates the very role that later, routine deploys authenticate *with*, so it can't
# bootstrap itself via that role's own OIDC trust.

terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }

  backend "s3" {
    bucket         = "releasetwin-terraform-state-846136340491"
    key            = "bootstrap/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "releasetwin-terraform-state-lock"
    encrypt        = true
  }
}

provider "aws" {
  region = var.region
}

variable "region" {
  description = "AWS region for the deploy role (IAM is global, but kept consistent with the rest of this project's -var conventions)."
  type        = string
  default     = "us-east-1"
}

variable "github_repo" {
  description = "GitHub repo allowed to assume the deploy role via OIDC, as \"owner/repo\"."
  type        = string
  default     = "ernestoalejowitt22/ReleaseTwin"
}

variable "state_bucket_name" {
  description = "The hosted/terraform-state-backend bucket this role needs read/write access to (both for this config's own state and hosted/terraform's)."
  type        = string
  default     = "releasetwin-terraform-state-846136340491"
}

variable "state_lock_table_name" {
  description = "The hosted/terraform-state-backend lock table this role needs read/write access to."
  type        = string
  default     = "releasetwin-terraform-state-lock"
}

data "aws_s3_bucket" "terraform_state" {
  bucket = var.state_bucket_name
}

data "aws_dynamodb_table" "terraform_lock" {
  name = var.state_lock_table_name
}

# design.md Risks: if this account already has a GitHub Actions OIDC provider from another
# project, this resource will fail with "already exists" — `terraform import` the existing
# provider's ARN instead of creating a duplicate (the provider itself is account-wide and safe to
# share; the *role* below is what's actually scoped to this one repo).
data "tls_certificate" "github_actions" {
  url = "https://token.actions.githubusercontent.com/.well-known/openid-configuration"
}

resource "aws_iam_openid_connect_provider" "github_actions" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github_actions.certificates[0].sha1_fingerprint]

  # This provider is imported, not created here — it's shared with another existing project in
  # this account, whose thumbprint_list (multiple legacy entries) differs from whatever this
  # config's own `data "tls_certificate"` fetch happens to compute today. AWS validates GitHub's
  # OIDC certs against its own trusted root CAs, not the configured thumbprint, for well-known
  # issuers like this one — thumbprint_list is effectively vestigial here, so don't fight over it
  # (or need extra IAM permissions to update someone else's shared resource for no real benefit).
  lifecycle {
    ignore_changes = [thumbprint_list]
  }
}

data "aws_iam_policy_document" "github_actions_assume_role" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github_actions.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Any branch/tag/PR of this one repo — the workflow itself is workflow_dispatch-only (design.md
    # Non-Goals: no auto-deploy on push), so this doesn't grant deploy-on-push by itself.
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repo}:*"]
    }
  }
}

resource "aws_iam_role" "github_actions_deploy" {
  name               = "releasetwin-github-actions-deploy"
  assume_role_policy = data.aws_iam_policy_document.github_actions_assume_role.json
}

# design.md: least-privilege, not admin — scoped to exactly the resources hosted/terraform's own
# `apply` creates/manages (naming matches its `table_prefix = "releasetwin-dev-"` default; update
# both together if that ever changes), plus the state backend this role also needs for hosted/
# terraform's own remote state.
data "aws_iam_policy_document" "github_actions_deploy_permissions" {
  statement {
    sid = "DynamoDbTable"
    actions = [
      "dynamodb:CreateTable",
      "dynamodb:DeleteTable",
      "dynamodb:DescribeTable",
      "dynamodb:UpdateTable",
      "dynamodb:TagResource",
      "dynamodb:UntagResource",
      "dynamodb:ListTagsOfResource",
    ]
    resources = ["arn:aws:dynamodb:${var.region}:846136340491:table/releasetwin-dev-*"]
  }

  statement {
    sid = "LambdaFunction"
    actions = [
      "lambda:CreateFunction",
      "lambda:DeleteFunction",
      "lambda:GetFunction",
      "lambda:GetFunctionConfiguration",
      "lambda:UpdateFunctionCode",
      "lambda:UpdateFunctionConfiguration",
      "lambda:TagResource",
      "lambda:UntagResource",
      "lambda:ListTags",
      "lambda:CreateFunctionUrlConfig",
      "lambda:DeleteFunctionUrlConfig",
      "lambda:GetFunctionUrlConfig",
      "lambda:UpdateFunctionUrlConfig",
    ]
    resources = ["arn:aws:lambda:${var.region}:846136340491:function:releasetwin-dev-*"]
  }

  statement {
    sid = "LambdaExecutionRole"
    actions = [
      "iam:CreateRole",
      "iam:DeleteRole",
      "iam:GetRole",
      "iam:PutRolePolicy",
      "iam:DeleteRolePolicy",
      "iam:GetRolePolicy",
      "iam:AttachRolePolicy",
      "iam:DetachRolePolicy",
      "iam:ListRolePolicies",
      "iam:ListAttachedRolePolicies",
      "iam:TagRole",
      "iam:UntagRole",
    ]
    resources = ["arn:aws:iam::846136340491:role/releasetwin-dev-*"]
  }

  statement {
    sid       = "PassLambdaExecutionRole"
    actions   = ["iam:PassRole"]
    resources = ["arn:aws:iam::846136340491:role/releasetwin-dev-*"]

    condition {
      test     = "StringEquals"
      variable = "iam:PassedToService"
      values   = ["lambda.amazonaws.com"]
    }
  }

  statement {
    sid     = "TerraformStateBucket"
    actions = ["s3:GetObject", "s3:PutObject", "s3:ListBucket"]
    resources = [
      data.aws_s3_bucket.terraform_state.arn,
      "${data.aws_s3_bucket.terraform_state.arn}/*",
    ]
  }

  statement {
    sid       = "TerraformStateLock"
    actions   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:DeleteItem"]
    resources = [data.aws_dynamodb_table.terraform_lock.arn]
  }
}

resource "aws_iam_role_policy" "github_actions_deploy" {
  name   = "deploy-permissions"
  role   = aws_iam_role.github_actions_deploy.id
  policy = data.aws_iam_policy_document.github_actions_deploy_permissions.json
}

output "github_actions_role_arn" {
  value = aws_iam_role.github_actions_deploy.arn
}
