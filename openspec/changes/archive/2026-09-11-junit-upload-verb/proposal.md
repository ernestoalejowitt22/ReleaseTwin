## Why

The hosted platform now accepts a JUnit XML report at `POST /api/ingest/junit`, so a team with an
existing Playwright, pytest, or Jest suite can get run history onto the dashboard without authoring
a case file. Today the only way in is a hand-written `curl` with the right header, URL, and token
plumbing — which every such team would write for themselves, slightly differently.

The CLI already owns the other half of this conversation: it resolves `RELEASETWIN_API_URL` /
`RELEASETWIN_API_TOKEN` and uploads case and flag-proof reports through `IngestClient`. Uploading a
JUnit file is the same act with a different body.

## What Changes

- **`releasetwin upload-junit <file> [--release <label>]`**: reads a JUnit XML file and uploads it
  to the hosted platform, printing how many test cases were recorded and the dashboard URL for the
  project's run history. Non-zero exit on failure, with the platform's own rejection message shown
  verbatim.
- **`IngestClient` gains one method** that sends a raw XML body, reusing the client's existing base
  URL, bearer auth, and test handler seam.
- The verb is matched **before** `CliEntrypoint`'s fallthrough, which otherwise treats an
  unrecognized first argument as a directory of cases to run.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `cli-runner`: adds the upload-junit verb alongside the existing "Results are optionally uploaded
  to the hosted platform" behavior.

## Impact

- **CLI:** one new verb in `CliEntrypoint`, one new method on `IngestClient`, usage text, tests.
- **No core or adapter change.** The verb does not execute cases, load fixtures, or touch
  `ReleaseTwin.Core` — it reads a file and posts bytes.
- **No parsing.** The file is uploaded verbatim; the hosted parser is the single place JUnit dialect
  differences are handled.

## Explicitly not in this change

**No `junit_path` input on the GitHub Action.** The Action is deliberately hosted-free — its own
description says "no ReleaseTwin account or hosted call", and its `evidence` input repeats the
promise. An upload input would need a ReleaseTwin API token and would contradict both that
description and `ci-pr-integration`'s documented secret boundary. A CI user wanting an upload adds a
one-line `run:` step calling this verb, which costs the same YAML as a `junit_path` input would once
the token is passed anyway. Revisit only as a deliberate decision to change what the Action is.

## Manual steps

None.
