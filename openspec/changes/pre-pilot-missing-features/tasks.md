## 1. Export capability + archive builder

- [ ] 1.1 Add `OrgCapability.ExportData` to the enum; leave it out of the `member` / `viewer` arms of `OrgCapabilities.Allows` so it is admin-only by construction (mirror `ManageSharing`). Extend the `OrganizationAccessGuardTests` capability matrix.
- [ ] 1.2 `Services/DataExport/ExportArchiveBuilder.cs` — given an `organizationId`, produce a `byte[]` ZIP: `manifest.json`, `run-history.json` (`{ caseReports, flagProofReports }` with every `Uploaded*Report` field), `evidence/<reportId>.json` (verbatim `DocumentJson` + `{ screenshotIds, uploadedAt, reportKind }`), `screenshots/<blobId>.png` from `IEvidenceBlobStore`. A screenshot blob that returns null is skipped and recorded in `manifest.missingScreenshots`.
- [ ] 1.3 `manifest.json` shape: `formatVersion` (int, start at 1), `organization: { id, name }`, `generatedAt`, `counts: { caseReports, flagProofReports, evidenceDocuments, screenshots }`, `missingScreenshots: [...]`.
- [ ] 1.4 The builder reads only via `IProjectRepository.ListByOrganizationAsync` → per-project `ICaseReportRepository` / `IFlagProofReportRepository` / `IRunEvidenceRepository` `ListByProjectAsync`. No cross-org read path.

## 2. Archive store seam

- [ ] 2.1 `IExportArchiveStore { Task<string?> StoreAsync(byte[] zip, string fileName, CancellationToken) }` — returns a download URL or null.
- [ ] 2.2 `S3ExportArchiveStore` — `PutObject` to `s3://<evidence-bucket>/exports/<orgId>/<yyyyMMddTHHmmssZ>.zip`, return a 1-hour presigned GET URL. Bound in `Program.cs` only when the evidence bucket is configured (same condition `S3EvidenceBlobStore` uses).
- [ ] 2.3 No-op fallback (unbound `IExportArchiveStore`, or a null-returning default) for dev / tests.

## 3. Endpoint

- [ ] 3.1 `POST /api/export` (ClerkJwt, `Require(OrgCapability.ExportData)`). Build the archive for `currentOrg.OrganizationId`.
- [ ] 3.2 If `IExportArchiveStore.StoreAsync` returns a URL → `200 { downloadUrl, expiresAt }`. Otherwise → stream the ZIP in the body with `Content-Type: application/zip` and `Content-Disposition: attachment; filename="releasetwin-export-<org>-<date>.zip"`.
- [ ] 3.3 Register the endpoint in `Program.cs` (`app.MapExportEndpoints()` or fold into an existing group).

## 4. Terraform

- [ ] 4.1 `hosted/terraform/evidence.tf` (or `lambda.tf`): the API Lambda role already has `s3:PutObject` on `${evidence_blobs.arn}/*` — confirm it covers the `exports/` prefix (it does; the statement is `/*`). No IAM change expected; note it if a tighter scope exists.
- [ ] 4.2 Add an S3 lifecycle rule to the evidence bucket expiring `exports/` objects after 7 days.
- [ ] 4.3 `terraform validate` clean; no `terraform-bootstrap` change (the deploy role's `s3:*` on `releasetwin-dev-*` already covers a lifecycle configuration).

## 5. Web

- [ ] 5.1 A "Download your data" control on the dashboard (account/settings area or the members page), admin-only, gated on `canManage`.
- [ ] 5.2 `export-actions.ts` server action: `POST /api/export`; if the response is JSON with `downloadUrl`, return it for a client-side `window.location = url`; if it is `application/zip`, stream it through the BFF as a download.
- [ ] 5.3 Loading / error states (an export can take a few seconds).

## 6. Continuity copy reconciliation (design D6)

- [ ] 6.1 `docs/data-export.md` — document the archive layout and every field; state the format version and that it is stable within a major version.
- [ ] 6.2 `web/src/app/(marketing)/docs/security/page.tsx` — the "exportable at any time, in a documented format" line points at the real capability + `docs/data-export.md`.
- [ ] 6.3 `docs/continuity.md` — same export update; rework the *"the status page and SLA terms"* references so nothing asserts a present-tense status page / SLA that does not exist yet (e.g. "we notify affected accounts of incidents by email" — true today). Keep it in sync with the security page per the doc's own note.

## 7. Tests

- [ ] 7.1 `ExportArchiveBuilderTests` — archive contains every report across multiple projects; each evidence document verbatim; screenshots present; a metadata-only report appears in `run-history.json` with no `evidence/` file; a missing screenshot lands in `manifest.missingScreenshots`; the manifest counts are correct.
- [ ] 7.2 Shape-check test: the field names emitted in `run-history.json` match the `UploadedCaseReport` / `UploadedFlagProofReport` records (reflection or a golden file).
- [ ] 7.3 Secret-absence test: build an archive for an org that also has API tokens, adapter credentials, project secrets, and a Polar subscription id — assert none of those values appear anywhere in the ZIP bytes.
- [ ] 7.4 `ExportEndpointTests` (HTTP): admin gets a ZIP (dev path) or a `downloadUrl`; `member` and `viewer` get 403; a non-member gets 403; the archive for org A contains nothing from org B.
- [ ] 7.5 `S3ExportArchiveStore` unit test against the in-memory / localstack S3 fake if one exists, else assert the presigned-URL shape from a mocked `IAmazonS3`.

## 8. Verification

- [ ] 8.1 `dotnet build ReleaseTwin.sln` + `dotnet test` for the hosted project green; report the delta.
- [ ] 8.2 `cd web && npm run build` + `npx eslint src` clean.
- [ ] 8.3 `openspec validate pre-pilot-missing-features --strict`.
- [ ] 8.4 CI green on the branch.
- [ ] 8.5 **Needs the user to run this:** after deploy, request an export against a real org with evidence; open the ZIP; confirm the redacted evidence + screenshots are intact and the format matches `docs/data-export.md` (per the project's "the artifact IS the deliverable" rule).
