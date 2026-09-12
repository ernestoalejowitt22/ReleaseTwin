## Why

Every hosted upload from CI has needed a stored secret: `RELEASETWIN_API_TOKEN`, issued on the
project's Settings page and pasted into the CI's secret store. GitHub Actions already hands every
job a signed OIDC identity, and the hosted platform (releasetwin-platform change
`github-oidc-ingest`) now exchanges that identity for a short-lived ingest credential at
`POST /api/cli/auth/github`, provided the job names its project and the project is bound to the
job's repository. This is the CLI side: use that exchange automatically, keep the stored token as
the fallback for every other CI, and fail loudly when a job asked for OIDC and could not get it.

## What Changes

- **Credential resolution has one order, shared by `run` and `upload-junit`:** `RELEASETWIN_API_TOKEN`
  if set (unchanged, wins over everything); else, if `RELEASETWIN_PROJECT_ID` (or `project:` in
  `releasetwin.yml`) names a project and the job carries GitHub's `ACTIONS_ID_TOKEN_REQUEST_URL` /
  `_TOKEN` pair, request a GitHub token for audience `api.releasetwin.com` and exchange it; else no
  credential and no error, exactly as before.
- **Naming a project is intent, so failure is loud:** a missing `id-token: write` permission or a
  refused exchange prints one line with the fix and exits non-zero; `--summary-json` still gets
  written with `upload: { mode: "oidc-exchange-failed", reason }` so a PR comment can show it.
- **The summary records how the upload authenticated** (`upload.mode`: `token`, `oidc`, `none`) —
  schema version 4; the field is omitted when null so older consumers see the v3 shape.
- **`RELEASETWIN_API_URL` defaults to `https://api.releasetwin.com` on the OIDC path.** The stored-token
  path keeps its pre-existing placeholder default, byte for byte.
- **Docs lead with the token-free workflow**; the stored token moves under "Other CI systems".

## Capabilities

### Modified Capabilities
- `cli-runner`: "Results are optionally uploaded to the hosted platform" gains the OIDC path and the
  loud-failure rule; "The CLI can upload an existing JUnit report" accepts the same resolution.

## Out of scope
- GitLab, Bitbucket, Azure Pipelines OIDC (same shape, later).
- Removing the stored token.
