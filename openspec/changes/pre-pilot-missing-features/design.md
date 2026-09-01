## Context

See `proposal.md` — Why. The hosted API (`hosted/ReleaseTwin.Hosted.Api`) runs as
an AWS Lambda behind a Function URL; `web/` is the BFF. Relevant existing pieces:

- `IProjectRepository.ListByOrganizationAsync`, `ICaseReportRepository` /
  `IFlagProofReportRepository.ListByProjectAsync`, `IRunEvidenceRepository.
  ListByProjectAsync` — everything the export reads is already a per-project list.
- `UploadedRunEvidence.DocumentJson` is opaque, CLI-redacted JSON; screenshots
  live in `IEvidenceBlobStore` (S3 in prod via `S3EvidenceBlobStore`, filesystem
  locally, in-memory in tests).
- `IOrganizationAccessGuard.Require(OrgCapability)` + the static `OrgCapabilities.
  Allows` table gate every org-scoped endpoint; `admin`-only capabilities are just
  ones absent from the `member` / `viewer` arms.

Constraints:
- Lambda Function URL buffered responses cap at ~6 MB; an evidence export with a
  handful of PNG screenshots exceeds that immediately.
- CI-only Terraform via OIDC; the deploy role's permissions are managed in
  `hosted/terraform-bootstrap` and must be granted in the same PR as the infra.

## Goals / Non-Goals

**Goals:**
- One request produces a complete, self-describing archive of an org's run
  history + evidence.
- The download path is not bounded by Lambda response limits.
- The archive format is documented well enough to consume without ReleaseTwin.

**Non-Goals:**
- Incremental / delta export, scheduled export, or an export API for automation.
- Exporting journeys, project connections, adapter-credential *names*,
  notification targets, or share links — the continuity promise is specifically
  run history + evidence. (Journeys are customer-authored and a plausible later
  addition; noted, not built.)
- Emailing the download link — nice, but needs the transactional sender from
  `company-and-domain-launch`; defer.
- An async job queue. The build is synchronous inside the request.

## Decisions

### D1: ZIP archive, not one JSON blob

Layout:

```
manifest.json                 format version, org id + name, generatedAt, counts
run-history.json              { caseReports: [...], flagProofReports: [...] }  — every field
                              named exactly as the ingest contract (Uploaded*Report shapes)
evidence/<reportId>.json      the redacted DocumentJson verbatim + { screenshotIds, uploadedAt,
                              reportKind }
screenshots/<blobId>.png      each referenced screenshot, byte-for-byte
```

- **Why:** screenshots are binary — base64-inlining them into one JSON triples
  their size and makes the file un-streamable and awkward to consume. A ZIP with
  one file per concern is trivially inspected (`unzip`, any language's zip lib)
  and each file is independently valid JSON / PNG.
- **Alternative rejected:** NDJSON or a single JSON document. Simpler to emit,
  worse to consume, and forces base64 for images.

### D2: Build in-memory, upload to S3, return a presigned GET URL

`POST /api/export` (admin) → builds the ZIP in memory → if an export archive
store is configured (prod, S3), `PUT`s it to `s3://<evidence-bucket>/exports/<orgId>/<timestamp>.zip`
and returns `{ downloadUrl, expiresAt }` (presigned GET, 1 hour). The web
"Download your data" action follows the URL — the browser pulls the bytes
**directly from S3**, never back through the Lambda.

- **Why:** sidesteps the Function URL 6 MB cap and the ASP.NET-Core-on-Lambda
  response-streaming uncertainty entirely. The build still holds the ZIP in
  Lambda memory transiently — bounded by the org's retained evidence, fine at
  pilot scale; bump `memory_size` or switch to an S3 multipart streaming upload
  if it ever isn't.
- **Alternative rejected — Lambda RESPONSE_STREAM:** needs `InvokeMode` on the
  Function URL and hosting-layer support that isn't confirmed; still streams
  through the Lambda.
- **Alternative rejected — async job (queue → build → notify):** real machinery
  for zero current customers.

### D3: A tiny `IExportArchiveStore` seam so dev / tests don't need S3

```
IExportArchiveStore.StoreAsync(bytes, name) -> string? downloadUrl
```

- `S3ExportArchiveStore` — PUTs to the `exports/` prefix, returns a presigned URL.
  Bound only when the evidence bucket is configured (same condition
  `S3EvidenceBlobStore` already uses).
- Otherwise unbound → the endpoint streams the ZIP straight back in the response
  body with `Content-Disposition: attachment` (small in dev; how the HTTP tests
  read it).

The web BFF handles both: JSON body with `downloadUrl` → redirect; `application/zip`
body → pass through.

### D4: `OrgCapability.ExportData`, admin-only

One new enum value, absent from the `member` / `viewer` arms of
`OrgCapabilities.Allows` → admin-only by construction. Same move as
`ManageSharing`. The endpoint is `Require(OrgCapability.ExportData)`, so it also
inherits the "no active org / not a member → 403" behaviour.

### D5: S3 lifecycle auto-deletes `exports/`

A lifecycle rule on the evidence bucket expires `exports/` objects after 7 days —
exports are transient download artifacts, not storage. The presigned URL's 1-hour
expiry is the real access control; the lifecycle rule is cleanup.

### D6: Reconcile the continuity copy in the same change

`docs/continuity.md` and `web/src/app/(marketing)/docs/security/page.tsx`:
- the "exportable at any time, in a documented format" claim → point at the real
  endpoint + `docs/data-export.md`.
- `continuity.md`'s references to *"the status page and SLA terms"* → reworded to
  not assert a present-tense status page / SLA that doesn't exist (a plain
  "we publish incident notices to affected accounts by email" is true today).

No spec delta for `marketing-site` — the security page is content, not a governed
contract; this is a wording fix.

## Risks / Trade-offs

- **Large export → Lambda memory** → at pilot scale an org's total retained
  evidence is small; monitor, bump `memory_size`, escape hatch is D2's multipart
  streaming upload.
- **Presigned URL forwarded / leaked** → 1-hour expiry + `exports/` lifecycle +
  the URL is only ever handed to an authenticated admin. The archive contains
  only that admin's own org's already-redacted data.
- **Screenshot blob purged between listing and fetch** → skip it, record the
  omission in the manifest (`missingScreenshots: [...]`), don't fail the export.
- **Format drift** → `manifest.formatVersion`; `docs/data-export.md` is the
  contract; a shape-check test asserts the emitted `run-history.json` field names
  match the `Uploaded*Report` records.

## Migration Plan

New endpoint — no data migration. Rollout in one PR: endpoint + `IExportArchiveStore`
+ Terraform (`s3:PutObject` on `exports/*` for the API role, the lifecycle rule)
+ `docs/data-export.md` + the `continuity.md` / security-page wording + the web
button. `terraform-bootstrap` already grants `s3:*` on `releasetwin-dev-*` to the
deploy role, so no bootstrap change.

**Rollback:** the endpoint is additive and behind `OrgCapability.ExportData`;
nothing else changes behaviour. The doc wording can revert independently.

## Open Questions

- `exports/` S3 lifecycle window — 7 days is a guess; a config constant, doesn't
  affect the design.
- Whether `run-history.json` should also carry the per-report `FailureDetail`
  free-text (it's on `UploadedCaseReport`, is metadata, contains no secrets) —
  lean yes; decide when writing the serializer.
