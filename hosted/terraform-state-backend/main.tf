# hosted-platform-deployment design.md: the one truly irreducible "chicken" — the S3 bucket and
# DynamoDB lock table that every other terraform root (hosted/terraform-bootstrap,
# hosted/terraform) uses as its *own* remote state backend can't itself be remote-stated (nothing
# exists yet to point at). Kept in its own tiny root, separate from hosted/terraform-bootstrap, so
# only these two static, essentially-never-change-once-created resources carry the "state is local
# and lost after the CI run" tradeoff — not the OIDC provider/CI role, which are more likely to
# need a real update later and get their own durable remote state as of their very first apply
# (see hosted/terraform-bootstrap/main.tf's backend block).
#
# Run once (or again only if this exact layer changes) via the "state-backend" job in
# .github/workflows/bootstrap.yml, authenticated with a short-lived MFA session pasted into repo
# secrets for that one run — see that workflow for why this can't use OIDC (there's no CI role to
# assume yet; this is what creates the ground everything else, including that role, stands on).

terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.region
}

variable "region" {
  type    = string
  default = "us-east-1"
}

# Bucket names are globally unique across all of AWS, not just this account — the account ID
# suffix avoids collisions without needing a random suffix that would change on every apply.
variable "state_bucket_name" {
  type    = string
  default = "releasetwin-terraform-state-846136340491"
}

variable "state_lock_table_name" {
  type    = string
  default = "releasetwin-terraform-state-lock"
}

resource "aws_s3_bucket" "terraform_state" {
  bucket = var.state_bucket_name
}

resource "aws_s3_bucket_versioning" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_public_access_block" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_dynamodb_table" "terraform_lock" {
  name         = var.state_lock_table_name
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }
}

output "state_bucket_name" {
  value = aws_s3_bucket.terraform_state.bucket
}

output "state_bucket_arn" {
  value = aws_s3_bucket.terraform_state.arn
}

output "state_lock_table_name" {
  value = aws_dynamodb_table.terraform_lock.name
}

output "state_lock_table_arn" {
  value = aws_dynamodb_table.terraform_lock.arn
}
