## 1. The client method

- [x] 1.1 Add `UploadJUnitReportAsync(byte[] xml, string? release, CancellationToken)` to `IngestClient`, POSTing a raw `application/xml` body to `/api/ingest/junit` with `?release=` when a label is given (design D1)
- [x] 1.2 Do not route through `SendAsync`: inspect the status directly and read the response body on both success and failure, so the platform's rejection text survives (`EnsureSuccessStatusCode` discards it)
- [x] 1.3 Return a result carrying `Recorded`, `RunUrl`, and a failure message; parse them from the 201 body, tolerating a response that cannot be parsed the way the existing ack path does
- [x] 1.4 URL-encode the release label into the query string

## 2. The verb

- [x] 2.1 Add an `upload-junit` branch to `CliEntrypoint.ExecuteAsync`, matched **before** the fallthrough that treats an unrecognized head as a cases directory (design D2)
- [x] 2.2 Parse `<file>` and optional `--release <label>`; a missing file argument prints usage and exits non-zero
- [x] 2.3 Resolve `RELEASETWIN_API_TOKEN` and `RELEASETWIN_API_URL` the same way `CliRunner` does; a missing token exits non-zero naming the variable (design D4)
- [x] 2.4 Check the file exists and read its bytes before any network call; a missing file exits non-zero naming the path (design D3)
- [x] 2.5 On success print the recorded count and the run-history URL and exit zero; on failure print the platform's message and exit non-zero
- [x] 2.6 Add the verb and its `--release` option to the usage text

## 3. Tests

- [x] 3.1 A successful upload sends the file's bytes unmodified, as `application/xml`, to `/api/ingest/junit` with the bearer token
- [x] 3.2 `--release` reaches the request as a query parameter, URL-encoded
- [x] 3.3 The 201 body's `recorded` and `runUrl` are printed
- [x] 3.4 A rejection prints the platform's own message verbatim and exits non-zero
- [x] 3.5 A missing token exits non-zero, names the variable, and makes no network call
- [x] 3.6 A missing file exits non-zero, names the path, and makes no network call
- [x] 3.7 Invoking the verb never runs cases — pins the dispatch ordering the fallthrough would otherwise swallow (design D2)
- [x] 3.8 A document in an unfamiliar dialect is uploaded unchanged rather than rejected locally

## 4. Docs

- [x] 4.1 Add the verb to `src/ReleaseTwin.Cli/README.md` and the repo README's CLI section if it lists verbs, including that the file is uploaded as-is and the platform validates it

## 5. Verification

- [x] 5.1 `dotnet build ReleaseTwin.sln` and `dotnet test ReleaseTwin.sln` — report actual counts
- [x] 5.2 `openspec validate junit-upload-verb --strict`
- [x] 5.3 Run the verb against a real hosted API with a seeded token and a real Playwright JUnit file, and report what actually came back and what landed (CLAUDE.md — evidence quality). Needs the platform repo's DynamoDB Local and `seed-token`; pre-flight Docker first
