## Context

See proposal.md — *Why*. Current state on the `dashboard-evidence-viewer` branch:

- `IEvidenceBlobStore` (`Data/Store/IEvidenceBlobStore.cs`): `PutAsync(id, png)` / `GetAsync(id) → byte[]?` / `DeleteAsync(id)`. Two impls today — `FileSystemEvidenceBlobStore` (a directory), `InMemoryEvidenceBlobStore` (tests).
- `Program.cs:95`: `AddSingleton<IEvidenceBlobStore>(_ => new FileSystemEvidenceBlobStore(config["Evidence:BlobDirectory"] ?? Path.Combine(Path.GetTempPath(), "releasetwin-evidence-blobs")))`.
- `Program.cs:224`: `if (Environment.GetEnvironmentVariable("RELEASETWIN_LAMBDA_TASK") == "EvidencePurge") { … EvidencePurgeService.RunAsync(); return; }` — the entrypoint exists, nothing invokes it.
- `EvidencePurgeService.RunAsync`: `ListAllAsync` (table Scan) → group by project → for each expired doc, `_blobs.DeleteAsync(screenshotId)` then `_evidence.DeleteAsync(projectId, reportId)`. So the purge role needs table Scan + DeleteItem and S3 DeleteObject.
- `hosted/terraform/alerting.tf` already declares a second scheduled Lambda (`staleness_digest`): its own IAM role, a same-artifact `aws_lambda_function`, a `RELEASETWIN_LAMBDA_TASK` env discriminator, an `aws_cloudwatch_event_rule` (`rate(1 day)`), an `aws_cloudwatch_event_target`, and an `aws_lambda_permission`. This change copies that block.
- AWS SDK: the project is on the v4 line (`AWSSDK.DynamoDBv2` 4.0.5, `AWSSDK.SimpleNotificationService` 4.0.100). `IAmazonDynamoDB` and `IAmazonSimpleNotificationService` are both registered as singletons using the SDK's default credential chain.

## Goals / Non-Goals

**Goals**

- Evidence actually expires in production, on each project's own retention clock.
- Screenshot blobs survive across Lambda invocations and are deleted with their evidence document.
- Zero change to local dev, the test suite, or any `IEvidenceBlobStore` caller.

**Non-Goals**

- S3 bucket lifecycle rules — the app owns deletion timing (per-project window), a lifecycle rule would delete on a fixed age and break the "shortening the window makes old evidence eligible immediately" contract.
- Migrating existing filesystem blobs — there are none in production (evidence hasn't shipped).
- Object versioning, cross-region replication, CloudFront in front of the bucket — the dashboard serves blobs through its BFF proxy (`/dashboard/reports/[id]/evidence/screenshot/[id]`), not a public URL.
- Changing the purge cadence or making it configurable — `rate(1 day)` matches the staleness digest and `evidence-store` only says "recurring".

## Decisions

### D1: `S3EvidenceBlobStore`, selected by config, `IAmazonS3` registered only when configured

New `S3EvidenceBlobStore(IAmazonS3 s3, string bucket)` implementing the same three methods — `PutObject` (`image/png`), `GetObject` (return null on `NoSuchKey`), `DeleteObject` (idempotent). Key = the screenshot id verbatim (already a 32-hex-char id, S3-safe).

`Program.cs`:
```
var evidenceBucket = builder.Configuration["Evidence:BlobBucket"];
if (!string.IsNullOrWhiteSpace(evidenceBucket))
{
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(/* region from config, default cred chain */));
    builder.Services.AddSingleton<IEvidenceBlobStore>(sp => new S3EvidenceBlobStore(sp.GetRequiredService<IAmazonS3>(), evidenceBucket));
}
else
{
    // unchanged filesystem default
}
```

Alternative: always register `IAmazonS3` (like SNS is). Rejected — SNS is cheap to construct and its one use is guarded by a null topic ARN; an unused S3 client on every local/test run is avoidable noise, and the config gate is the same pattern `useRealDynamoDb` already uses.

### D2: One bucket, `${table_prefix}` prefixed, private, SSE-S3

Matches the table's own `${var.table_prefix}ReleaseTwinHosted` naming so a prod/staging split is one variable. `aws_s3_bucket_public_access_block` all-true; `aws_s3_bucket_server_side_encryption_configuration` with `AES256` (KMS is overkill — the blobs are already CLI-redacted screenshots, and SSE-S3 is free). No lifecycle rule (see Non-Goals).

### D3: Purge Lambda IAM is read-write on the table (unlike the staleness digest)

The staleness digest is read-only (`Query` + `Scan`). The purge **writes** — `dynamodb:DeleteItem` on the table, plus `Scan`/`Query` to find expired rows and read each project's retention window. Plus S3 `GetObject`? No — purge only deletes, so `s3:DeleteObject` + `s3:ListBucket` on the bucket. The `hosted_api` role separately gets `s3:PutObject` + `s3:DeleteObject` (delete: not strictly needed today, but cheap and covers a future "delete this evidence now" endpoint).

### D4: No dedicated `S3EvidenceBlobStore` unit test — same convention as `SnsOperatorAlertPublisher`

Originally this planned a hand-written `IAmazonS3` stub. In practice the v4 `IAmazonS3` interface has ~250 members, so a hand stub is impractical, and the repo deliberately uses no mocking library (every fake is hand-written: `InMemoryHostedTable`, `InMemoryEvidenceBlobStore`, `InMemoryOperatorAlertPublisher`).

`S3EvidenceBlobStore` is a 3-call SDK passthrough — exactly the shape of `SnsOperatorAlertPublisher`, which has no unit test either. What matters (purge deletes blobs, ingest stores them, the dashboard serves them) is already covered by `EvidenceStoreTests` / `EvidenceIngestApiTests` running against `InMemoryEvidenceBlobStore`. The S3 path's correctness is covered by `terraform plan` (the bucket + IAM exist) and a manual smoke check on first deploy.

## Risks / Trade-offs

- **`AWSSDK.S3` v4 availability** — the project's other AWS packages are v4; `AWSSDK.S3` needs the matching major. If only a v3 is restorable (private feed is unreachable in this environment), pin the newest v3 that co-exists with `AWSSDK.Core` v4 — the three IO calls used here are stable across both. → Resolve during apply; note the exact version in the csproj comment.
- **Two Lambdas, one artifact, now three tasks** (HTTP, StalenessDigest, EvidencePurge) — the `RELEASETWIN_LAMBDA_TASK` switch in `Program.cs` stays a flat `if` chain; fine at three, revisit if it grows.
- **Purge Scan cost** — same as the staleness digest's, accepted at current scale; the purge additionally does N `GetAsync`-free `DeleteObject` calls. Bounded by expired-evidence count, once a day.
- **A blob orphaned by a failed mid-purge delete** — if `DeleteObject` succeeds but the subsequent `_evidence.DeleteAsync` throws, the row stays and is retried next run (idempotent). If the row delete succeeds but a blob delete failed earlier, the blob is orphaned — acceptable (storage-only cost); a future sweep could reconcile `ListBucket` against live `ScreenshotIds`.

## Migration Plan

1. `S3EvidenceBlobStore` + `Program.cs` config gate + csproj package. Build + test green with no S3 config (unchanged path).
2. `S3EvidenceBlobStore` stub test.
3. `hosted/terraform/evidence.tf` — bucket + `evidence_purge` Lambda + schedule + IAM; `lambda.tf` `hosted_api` S3 access + `Evidence__BlobBucket` env. `terraform validate`.
4. Operator doc note.
5. Deploy order (post PR #1 merge): `terraform apply` (creates bucket + purge Lambda), then redeploy the API package so `hosted_api` picks up `Evidence__BlobBucket`.

Rollback: unset `Evidence:BlobBucket` → the API falls back to the filesystem store (blobs written to S3 become unreadable, but none exist yet); `terraform destroy -target` the purge Lambda + bucket. No data migration either direction.

## Open Questions

- Exact `AWSSDK.S3` version — pick the v4 that matches `AWSSDK.Core` at apply time.
- Whether to fold the terraform into `alerting.tf` (it's "operator infra" too) or a new `evidence.tf` — cosmetic; `evidence.tf` keeps `alerting.tf` about alerting.
