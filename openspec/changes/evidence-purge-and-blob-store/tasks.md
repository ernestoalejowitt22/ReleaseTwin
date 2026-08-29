## 1. S3 blob store

- [ ] 1.1 Add `AWSSDK.S3` to `hosted/ReleaseTwin.Hosted.Api.csproj` (version matching the `AWSSDK.Core` v4 line the other AWS packages use; note the resolved version in a csproj comment)
- [ ] 1.2 `S3EvidenceBlobStore : IEvidenceBlobStore` in `Data/Store/` — `PutObjectAsync` (`image/png`), `GetObjectAsync` returning null on `NoSuchKey`, idempotent `DeleteObjectAsync`; key = the screenshot id verbatim
- [ ] 1.3 `Program.cs` — when `Evidence:BlobBucket` is set, register `IAmazonS3` (default credential chain, region from `Aws:Region`) and `S3EvidenceBlobStore`; otherwise keep the existing `FileSystemEvidenceBlobStore` default unchanged
- [ ] 1.4 Build + full hosted test suite green with **no** S3 config (the filesystem/in-memory paths are untouched)

## 2. S3 blob store test

- [ ] 2.1 Hand-written `IAmazonS3` stub (only `PutObjectAsync`/`GetObjectAsync`/`DeleteObjectAsync` over a dictionary; `AmazonS3Exception { ErrorCode = "NoSuchKey" }` on a missing get)
- [ ] 2.2 `S3EvidenceBlobStoreTests` — put→get round-trip, get of a missing key returns null, delete then get returns null, delete of a missing key does not throw

## 3. Terraform

- [ ] 3.1 `hosted/terraform/evidence.tf` — private S3 bucket `${var.table_prefix}releasetwin-evidence-blobs`: `aws_s3_bucket`, `aws_s3_bucket_public_access_block` (all true), `aws_s3_bucket_server_side_encryption_configuration` (AES256). No lifecycle rule.
- [ ] 3.2 Same file — `aws_lambda_function.evidence_purge` (same `filename`/`source_code_hash` as `hosted_api`, `handler = "ReleaseTwin.Hosted.Api"`, `runtime = "dotnet10"`, `timeout = 60`, env `RELEASETWIN_LAMBDA_TASK = "EvidencePurge"` + `Aws__Region` + `Aws__DynamoDb__TablePrefix` + `Evidence__BlobBucket`), mirroring `alerting.tf`'s `staleness_digest`
- [ ] 3.3 Purge IAM role — assume-role for lambda, `AWSLambdaBasicExecutionRole`, an inline policy for DynamoDB `Query`/`Scan`/`DeleteItem` on the table (+ `/index/*`), and an inline policy for S3 `DeleteObject`/`ListBucket` on the new bucket
- [ ] 3.4 `aws_cloudwatch_event_rule` (`rate(1 day)`) + `aws_cloudwatch_event_target` + `aws_lambda_permission` for the purge Lambda — same shape as the staleness digest
- [ ] 3.5 `hosted/terraform/lambda.tf` — add S3 `PutObject`/`DeleteObject` on the bucket to the `hosted_api` role, and `Evidence__BlobBucket = aws_s3_bucket.evidence_blobs.id` to its environment
- [ ] 3.6 `output "evidence_blob_bucket"` = the bucket name
- [ ] 3.7 `terraform -chdir=hosted/terraform validate` passes (and `fmt -check`)

## 4. Docs

- [ ] 4.1 Add a section to `docs/operator-alerting.md` (or a new `docs/evidence-storage.md`) — the purge Lambda, the bucket, that retention is per-project and the daily job enforces it, and any manual `terraform apply` / redeploy ordering

## 5. Validation

- [ ] 5.1 `openspec validate evidence-purge-and-blob-store --strict` passes
- [ ] 5.2 Full .NET solution build + all test projects green
- [ ] 5.3 `terraform validate` + `terraform plan` (against a throwaway workspace or with fake vars) shows only the new bucket, purge Lambda, IAM, and the `hosted_api` env/policy additions — nothing destroyed
