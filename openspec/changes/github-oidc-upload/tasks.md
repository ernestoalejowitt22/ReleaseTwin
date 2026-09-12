## 1. Resolver and runner

- [x] 1.1 `Upload/UploadCredentialResolver.cs`: order per D1; GitHub token request (`audience`, bearer), exchange `POST /api/cli/auth/github`, per-status failure messages naming the fix; OIDC path defaults the API URL to `https://api.releasetwin.com`, stored-token path keeps the placeholder.
- [x] 1.2 `CliRunner`: resolver replaces the token read; failure prints `ERROR: hosted upload could not authenticate: …`, writes the failed summary (`WriteFailedUploadSummary`), returns 1; `apiToken`/`apiUrl` locals unchanged downstream.
- [x] 1.3 `ProjectManifestDto.Project` + `CaseFileLoader.ReadManifestProjectId`; `CliRunner.WithManifestProjectId` folds it into the environment (environment wins).
- [x] 1.4 `RunSummary.Upload` (`RunSummaryUpload(mode, reason)`), schema version 4, omitted when null; `RunSummaryBuilder.Build(runUrl, upload)`.
- [x] 1.5 `JUnitUploadCommand`: resolver; "requires RELEASETWIN_API_TOKEN, or RELEASETWIN_PROJECT_ID on a GitHub Actions job with `permissions: id-token: write`" when nothing resolves; failed exchange exits 1 with the reason.
- [x] 1.6 Tests `GitHubOidcUploadTests.cs` (14): precedence, no-network for stored token, exchange request shape (audience, bearer, body), every refusal message, GitHub refusing, malformed id, runner uploads with the exchanged credential and never prints it, `upload.mode` for `oidc`/`token`/`none`, loud failure with the explanatory summary, manifest `project:` and env precedence, `upload-junit` all three paths. CLI test project 268 green (schema-version assertions moved to 4).

## 2. Docs

- [x] 2.1 `docs/ci.md` "Credentials": OIDC snippet first (`permissions: id-token: write`, `RELEASETWIN_PROJECT_ID` or manifest `project:`), stored token under "Other CI systems / no OIDC"; `#github-oidc` anchor the Action links to.
- [x] 2.2 `README.md` and `docs/hosted.md`: one sentence each that a GitHub Actions job needs no stored secret.

## 3. Release

- [ ] 3.1 Tag a CLI release so the image carries the resolver; the Action (releasetwin-action) pins the new digest. **After** the platform's exchange endpoint is deployed.
- [ ] 3.2 `dotnet test ReleaseTwin.sln`; `openspec validate github-oidc-upload --strict`; confirm with the user before archiving.
