## 1. Export capability + archive builder

- [x] 1.1 `OrgCapability.ExportData` added between `ManageSharing` and `UseProjects`; absent from the `Member`/`Viewer` arms of `OrgCapabilities.Allows` → admin-only. Guard matrix test extended in Group 7.
- [x] 1.2 `Services/DataExport/ExportArchiveBuilder.cs` — `BuildAsync(orgId)` → `byte[]` ZIP. `run-history.json` = `{ caseReports, flagProofReports }` as `ExportCaseReport`/`ExportFlagProofReport` records (every entity field + `projectId`/`projectName`, incl. `FailureDetail` per design open-question). `evidence/<reportId>.json` = verbatim `DocumentJson` + `{ reportKind, uploadedAt, screenshotIds }`. `screenshots/<blobId>.png` from `IEvidenceBlobStore`; a null blob → skipped + recorded in `manifest.missingScreenshots`.
- [x] 1.3 `manifest.json`: `formatVersion` (1), `generatedAt`, `organization {id,name}`, `counts {caseReports, flagProofReports, evidenceDocuments, screenshots}`, `missingScreenshots []`. Written last so counts reflect what was actually emitted.
- [x] 1.4 Reads only `IProjectRepository.ListByOrganizationAsync(orgId)` then per-project `ListByProjectAsync` on the three report/evidence repos — no cross-org path.

## 2. Archive store seam

- [x] 2.1 `IExportArchiveStore.StoreAsync(byte[] zip, string fileName, CancellationToken) -> ExportDownload?` (`{ DownloadUrl, ExpiresAt }` or null).
- [x] 2.2 `S3ExportArchiveStore` — `PutObject` to `exports/<orgId>/<file>.zip` in the evidence bucket, returns a 1-hour presigned GET URL with a `Content-Disposition: attachment` override. Bound in `Program.cs` inside the existing `Evidence:BlobBucket` branch, reusing the `IAmazonS3` singleton.
- [x] 2.3 `NullExportArchiveStore` (returns null) bound in the else branch — dev/tests get the streamed-ZIP path.

## 3. Endpoint

- [x] 3.1 `Endpoints/ExportEndpoints.cs` — `POST /api/export` (ClerkJwt), `currentOrg.Require(OrgCapability.ExportData)`, builds for the active org.
- [x] 3.2 `downloadUrl` present → `200 { downloadUrl, expiresAt }`; else `Results.File(zip, "application/zip", "releasetwin-export-<org>-<ts>.zip")`.
- [x] 3.3 `app.MapExportEndpoints()` after `MapShareLinkEndpoints()`; `ExportArchiveBuilder` registered scoped.

## 4. Terraform

- [x] 4.1 Confirmed — `hosted_api_evidence_s3` grants `s3:PutObject`/`GetObject` on `${evidence_blobs.arn}/*`, which covers `exports/*`. Presigning needs no extra IAM (the URL carries a delegated `GetObject`). No policy change.
- [x] 4.2 `aws_s3_bucket_lifecycle_configuration.evidence_blobs_exports` in `evidence.tf` — a single `Enabled` rule, `filter { prefix = "exports/" }`, `expiration { days = 7 }`. Screenshot blobs (prefix-less keys) untouched, so the bucket comment's "the app owns deletion timing" still holds.
- [x] 4.3 `terraform validate` clean. No `terraform-bootstrap` change — deploy role's `s3:*` on `releasetwin-dev-*` already covers a lifecycle configuration.

## 5. Web

- [x] 5.1 "Download your data" card on `/dashboard/members`, `isAdmin`-only, linking to `/dashboard/export`.
- [x] 5.2 `web/src/app/dashboard/export/route.ts` — a GET route handler: `POST`s the hosted `/api/export` with the Clerk token + `X-Org-Id`; JSON `{downloadUrl}` → `Response.redirect(url, 303)`; `application/zip` → pass the body through with the `Content-Disposition`. Plain `<a href>` triggers it — browser handles the download either way.
- [x] 5.3 Handled by the browser's own navigation state (the link is a full nav to the route handler); a 403 renders the handler's plain-text "Only an admin can export…" message.

## 6. Continuity copy reconciliation (design D6)

- [x] 6.1 `docs/data-export.md` — full field-by-field documentation of `manifest.json` / `run-history.json` / `evidence/*.json` / `screenshots/*.png`, states `formatVersion` 1 and additive-within-major.
- [x] 6.2 Security page "exportable" bullet rewritten to point at the dashboard control + a link to `docs/data-export.md`.
- [x] 6.3 `docs/continuity.md` "Your data is portable" updated to the real one-ZIP export; the `## What this does not cover` line no longer asserts a status page / SLA — replaced with "no formal SLA or public status page yet; we notify affected accounts of incidents by email".

## 7. Tests

- [x] 7.1 `ExportArchiveBuilderTests.ArchiveContainsEveryReportAndEvidenceDocumentAcrossProjects` — 2 projects, case+flag-proof reports, one with evidence + a real + a missing screenshot, one metadata-only. Asserts run-history completeness + project names, verbatim evidence document, screenshot bytes, `missingScreenshots`, all manifest counts.
- [x] 7.2 `RunHistoryFieldNamesMatchTheIngestContract` — reflection: `ExportCaseReport`'s fields == `UploadedCaseReport`'s (modulo `Id`→`ReportId` + added `ProjectName`).
- [x] 7.3 `ArchiveContainsNoSecretShapedData` — org with a token hash, adapter cred blob, project secret blob, and Polar `cus_`/`sub_` ids; asserts none of those strings appear anywhere in the ZIP bytes.
- [x] 7.4 `ExportEndpointTests` — admin `POST /api/export` → 200 `application/zip` with `manifest.json` + `run-history.json` (dev / Null-store path); `Member` and `Viewer` → 403. Org-scoping covered by `ArchiveIsScopedToOneOrganization`. Guard matrix extended for `ExportData`.
- [x] 7.5 `NullExportArchiveStore` returns null (covered implicitly by the dev-path endpoint test). `S3ExportArchiveStore` (PutObject + presign) is exercised end-to-end by 8.5 — no mocking library in the test project and `IAmazonS3` has no practical hand-stub.

## 8. Verification

- [x] 8.1 `dotnet build ReleaseTwin.sln` clean; engine tests all green (7 assemblies); hosted `dotnet test` **343 green** (+10: 4 guard-matrix rows, 4 `ExportArchiveBuilderTests`, 3 `ExportEndpointTests` cases).
- [x] 8.2 `cd web && npm run build` — compiled clean, `/dashboard/export` route registered; `npx eslint src` exit 0.
- [x] 8.3 `openspec validate pre-pilot-missing-features --strict` — valid.
- [x] 8.4 PR #59 — CI green (`build-and-test` x2, `build-test-lint`, `gitleaks`, Vercel).
- [ ] 8.5 **Needs the user to run this:** after deploy, request an export against a real org with evidence; open the ZIP; confirm the redacted evidence + screenshots are intact and the format matches `docs/data-export.md` (per the project's "the artifact IS the deliverable" rule).
