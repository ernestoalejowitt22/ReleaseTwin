# usage-metering tasks.md 1.4: provisions the single ReleaseTwinHosted table against real AWS.
# Not applied automatically against production — Program.cs only auto-provisions against DynamoDB
# Local (see hosted/ReleaseTwin.Hosted.Api/Data/Store/TableProvisioning.cs, the single source of
# truth this module mirrors). Requires AWS credentials configured for the `aws` provider (the
# standard credential chain — no hardcoded keys, same rule this project applies everywhere).
#
# Usage:
#   cd hosted/terraform
#   terraform init
#   terraform apply -var="table_prefix=releasetwin-hosted-prod-" -var="region=us-east-1"

terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # hosted-platform-deployment: bumped from ~> 5.0 — the "dotnet10" Lambda runtime identifier
      # (lambda.tf) isn't recognized by the AWS provider's schema until 6.x (verified empirically:
      # 5.100.0, the latest 5.x release, rejects it; 6.x accepts it). Safe to bump here since no
      # state has ever been applied against the 5.x provider — nothing to migrate.
      version = "~> 6.0"
    }
  }

  # hosted-platform-deployment design.md: remote state, because `terraform apply` runs from
  # ephemeral GitHub Actions runners with no memory of a local state file between runs. Bucket and
  # lock table are provisioned by hosted/terraform-bootstrap (applied once, manually, under MFA —
  # see that directory) — this block assumes they already exist rather than creating them itself,
  # avoiding a circular dependency on its own bootstrap.
  backend "s3" {
    bucket         = "releasetwin-terraform-state-846136340491"
    key            = "hosted/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "releasetwin-terraform-state-lock"
    encrypt        = true
  }
}

provider "aws" {
  region = var.region
}

variable "region" {
  description = "AWS region to provision the table in."
  type        = string
  default     = "us-east-1"
}

variable "table_prefix" {
  description = "Prefix for the table name, matching Aws:DynamoDb:TablePrefix in the API's own configuration (e.g. \"releasetwin-hosted-prod-\")."
  type        = string
  default     = ""
}

resource "aws_dynamodb_table" "hosted" {
  name         = "${var.table_prefix}ReleaseTwinHosted"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "GSI1PK"
    type = "S"
  }

  attribute {
    name = "GSI1SK"
    type = "S"
  }

  attribute {
    name = "GSI2PK"
    type = "S"
  }

  attribute {
    name = "GSI2SK"
    type = "S"
  }

  # design.md: serves ApiToken "list by project" (dashboard listing) — eventually consistent, never
  # used for the strongly-consistent token-auth check, which reads the table's own primary key.
  global_secondary_index {
    name            = "GSI1"
    hash_key        = "GSI1PK"
    range_key       = "GSI1SK"
    projection_type = "ALL"
  }

  # design.md: serves ApiToken "find by id" for the revoke-by-id two-step (Query GSI2, then
  # UpdateItem on the primary table).
  global_secondary_index {
    name            = "GSI2"
    hash_key        = "GSI2PK"
    range_key       = "GSI2SK"
    projection_type = "ALL"
  }

  # billing (design.md D3): the ProcessedBillingEvent idempotency items carry an epoch-seconds
  # ExpiresAt and self-delete ~30d after processing — comfortably past Polar's retry window. No other
  # item type sets ExpiresAt, so enabling TTL table-wide is harmless for them.
  ttl {
    attribute_name = "ExpiresAt"
    enabled        = true
  }
}

output "table_name" {
  value = aws_dynamodb_table.hosted.name
}

output "table_arn" {
  value = aws_dynamodb_table.hosted.arn
}
