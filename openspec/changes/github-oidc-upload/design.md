# github-oidc-upload — design

Companion to releasetwin-platform `github-oidc-ingest` (its design D5 is the contract this
implements). Only the CLI-side decisions are recorded here.

## D1. One resolver, folded in where the token was read

`Upload/UploadCredentialResolver.ResolveAsync(get, handler)` replaces the two places that read
`RELEASETWIN_API_TOKEN` (`CliRunner` before building the `IngestClient`, and `JUnitUploadCommand`).
It returns `(Mode, Token, ApiUrl, FailureReason)`; the runner keeps its `apiToken` / `apiUrl` locals
so the evidence-config, adapter-credential, and project-secret fetches are unchanged and simply
receive whichever credential resolved. The same `uploadHandlerForTesting` handler serves GitHub's
token endpoint, the exchange, and ingest in tests, routed by URL.

## D2. The manifest `project:` is folded into the environment

`CliRunner.RunAsync` reads `project:` from the cases directory's `releasetwin.yml`
(`CaseFileLoader.ReadManifestProjectId`, lenient: a broken manifest is reported by the strict load
that follows, never by credential resolution) and adds it as `RELEASETWIN_PROJECT_ID` to a copy of
the environment when the environment has none. Resolution then has one input. Journeys (which run
from the working directory) rely on the environment only.

## D3. Loud failure, but the summary is still written

Naming a project is declared intent. On an exchange failure the runner prints one `ERROR:` line
with the fix, writes the requested summary as `overall: failed`, zero cases, and
`upload: { mode: "oidc-exchange-failed", reason }`, and returns 1 before loading cases. The Action's
comment can then show the reason instead of an empty run. Summary schema goes to 4; `upload` is
omitted when null so a v3 consumer sees the old shape.

## D4. URL defaults

The stored-token path keeps `https://api.releasetwin.example` when no URL is configured, so existing
behaviour and tests are byte-identical. The OIDC path defaults to `https://api.releasetwin.com`,
because a job that names a project and has no URL configured is a real customer on the hosted
platform.

## Verified assumptions

- GitHub's request protocol and claim names: confirmed by the spike recorded in the platform
  change's tasks 0.1 (audience is echoed verbatim; 300 s lifetime).
- `JUnitUploadCommand` takes a `handlerForTesting`; `CliRunner.RunAsync` takes
  `uploadHandlerForTesting` — both reused for the exchange, so no new test seam.
