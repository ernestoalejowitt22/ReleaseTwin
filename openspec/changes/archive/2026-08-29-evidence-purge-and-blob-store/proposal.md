## Why

`dashboard-evidence-viewer` specified evidence retention and purge behavior (`evidence-store`: "Expired evidence is purged … by a recurring purge"; "Evidence is not visible across organizations") and shipped an `EvidencePurgeService` plus a `RELEASETWIN_LAMBDA_TASK=EvidencePurge` entrypoint. But two operational pieces are missing, so in production the specified behavior does not actually happen:

1. **The purge job has no schedule.** Nothing invokes `EvidencePurgeService` — evidence never expires on a real deploy.
2. **The blob store cannot run on Lambda.** `FileSystemEvidenceBlobStore` writes to local disk, which is ephemeral per Lambda invocation and not shared across concurrent instances. A screenshot blob written by the ingest function on one request is gone by the next; the purge function (a separate Lambda) can never see the blobs it is meant to delete.

This change makes the already-specified behavior real: an S3-backed blob store and a scheduled purge Lambda, mirroring the `operator-alerting` staleness-digest pattern.

## What Changes

- **New `S3EvidenceBlobStore : IEvidenceBlobStore`** — get/put/delete a PNG against one private bucket, keyed by screenshot id. Selected at composition when `Evidence:BlobBucket` configuration is present; `FileSystemEvidenceBlobStore` stays the default for local dev; `InMemoryEvidenceBlobStore` stays for tests. The `IEvidenceBlobStore` contract is unchanged, so no caller (`EvidenceIngestService`, `EvidencePurgeService`, the dashboard screenshot endpoint) changes.
- **Terraform (`hosted/terraform/`)**:
  - A private S3 bucket (`${table_prefix}releasetwin-evidence-blobs`), public access blocked, SSE-S3, no bucket lifecycle rule (the app owns deletion, per `evidence-store`'s retention contract — a lifecycle rule would delete on a different clock than the per-project window).
  - A second `aws_lambda_function` `evidence_purge` pointing at the same deployment artifact, `RELEASETWIN_LAMBDA_TASK=EvidencePurge`, plus its EventBridge daily schedule + invoke permission — the exact shape `alerting.tf`'s `staleness_digest` already uses.
  - IAM for the purge role: DynamoDB `Query` + `Scan` **and** `PutItem`/`DeleteItem` (the purge deletes evidence rows — unlike the read-only staleness digest), and S3 `GetObject`/`DeleteObject`/`ListBucket` on the new bucket.
  - The existing `hosted_api` role gains S3 `PutObject`/`DeleteObject` on the bucket (it writes blobs on ingest), and `Evidence__BlobBucket` in its environment.
- **Docs** — a short note in `docs/` (or `docs/operator-alerting.md`, which already documents the digest Lambda) covering the purge Lambda, the bucket, and the one manual step if any.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — `evidence-store`'s retention/purge/org-scoping requirements already specify this behavior; this change is the deployment plumbing that makes them run in production, the same category as `hosted-platform-deployment`. `.openspec.yaml` sets `skip_specs: true`.)

## Impact

- **`hosted/ReleaseTwin.Hosted.Api`**: new `S3EvidenceBlobStore` (needs an S3 SDK package — `AWSSDK.S3`, matching the v4 line the project already uses for DynamoDB/SNS); `Program.cs` blob-store registration branches on `Evidence:BlobBucket`; register `IAmazonS3` when configured (default SDK credential chain, same as DynamoDB/SNS).
- **`hosted/terraform/`**: new `evidence.tf` (or additions to `alerting.tf`) — S3 bucket + `evidence_purge` Lambda + EventBridge rule + IAM; `lambda.tf`'s `hosted_api` role/env gains S3 access + `Evidence__BlobBucket`.
- **Tests**: `S3EvidenceBlobStore` unit-tested against a stub `IAmazonS3` (put→get→delete round-trip, missing-key returns null); existing `EvidenceStoreTests` (in-memory) unchanged; a `terraform validate` check.
- **Docs**: one operator note.
- **No behavior change** for local dev or the test suite — the S3 path only activates when `Evidence:BlobBucket` is set.
- **Depends on** `dashboard-evidence-viewer` (PR #1) — `EvidencePurgeService`, `IEvidenceBlobStore`, `RELEASETWIN_LAMBDA_TASK=EvidencePurge` all live there. Not deployable until that merges and redeploys.
