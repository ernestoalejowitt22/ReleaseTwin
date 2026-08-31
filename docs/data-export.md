# Data export format

An organization admin can download the organization's complete run history and
stored evidence at any time — dashboard → **Download your data**, or
`POST /api/export` via the hosted API. This documents what the archive contains,
so you can consume it with your own tools and no ReleaseTwin-specific knowledge.

The export is generated on demand from current stored data. It contains only the
metadata the ingest API already accepts plus evidence documents **already
redacted by your CLI before upload** — never fixture file contents, request or
response bodies beyond a redacted evidence document, API tokens, adapter
credentials, project secrets, or billing identifiers.

## Archive layout

A single ZIP:

```
manifest.json
run-history.json
evidence/<reportId>.json          — one per report that has an uploaded evidence document
screenshots/<screenshotId>.png    — one per referenced screenshot that is still stored
```

### `manifest.json`

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | integer | This document describes version `1`. Fields are only added within a major version; a breaking layout change bumps this. |
| `generatedAt` | ISO-8601 string | When the archive was built. |
| `organization.id` | UUID | The exported organization. |
| `organization.name` | string | Its display name at export time. |
| `counts.caseReports` | integer | Number of case reports in `run-history.json`. |
| `counts.flagProofReports` | integer | Number of flag-proof reports in `run-history.json`. |
| `counts.evidenceDocuments` | integer | Number of `evidence/*.json` files. |
| `counts.screenshots` | integer | Number of `screenshots/*.png` files actually written. |
| `missingScreenshots` | array of string | Screenshot ids referenced by an evidence document but no longer stored (e.g. purged past a retention window). Not written to `screenshots/`. |

### `run-history.json`

```json
{ "caseReports": [ ... ], "flagProofReports": [ ... ] }
```

**Case report** — mirrors the ingest `case-report` contract, plus the owning project:

| Field | Notes |
|---|---|
| `reportId` | UUID |
| `projectId`, `projectName` | the project this report belongs to |
| `caseId`, `oracleLocator`, `fixtureSha256` | as uploaded |
| `passed` | boolean |
| `classification` | failure classification, or null |
| `failureDetail` | free-text failure detail, or null |
| `release` | the optional release label, or null |
| `cleanupStatus` | e.g. `AllSucceeded` |
| `durationMs` | integer |
| `uploadedAt` | ISO-8601 |

**Flag-proof report**:

| Field | Notes |
|---|---|
| `reportId`, `projectId`, `projectName` | as above |
| `caseId`, `oracleLocator`, `buildIdentity` | as uploaded |
| `outcome` | `Passed` / `Failed` / `Ineligible` |
| `knownBadLegPassed`, `knownGoodLegPassed` | boolean or null |
| `release` | or null |
| `uploadedAt` | ISO-8601 |

A report that was uploaded without an evidence document still appears here; it
simply has no `evidence/<reportId>.json` file.

### `evidence/<reportId>.json`

```json
{
  "reportId": "…",
  "reportKind": "case" | "flag-proof",
  "uploadedAt": "…",
  "screenshotIds": ["…"],
  "document": { … }
}
```

`document` is the evidence document **exactly as your CLI redacted it before
upload** — the hosted platform stores it opaquely and never modifies it. Its
internal shape (legs, steps, assertions, adapter evidence) is the CLI's evidence
format, not this document's concern.

### `screenshots/<screenshotId>.png`

Each screenshot referenced by an evidence document's `screenshotIds`, byte-for-byte
as uploaded (best-effort redacted in your CLI). Ids that appear in
`manifest.missingScreenshots` are not written.
