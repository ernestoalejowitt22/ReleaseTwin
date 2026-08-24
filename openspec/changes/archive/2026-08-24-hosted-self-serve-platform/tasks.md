## 1. Resolve deferred operational decisions (design.md Open Questions)

- [x] 1.1 Hosting provider deliberately deferred to deploy time — ASP.NET Core + Postgres is provider-agnostic; not a blocker for writing the code. Requires your own cloud account whenever it's chosen.
- [x] 1.2 Decided: GitHub OAuth only for Stage 1 (no magic-link email, no email-sending infra needed). **You still need to register a GitHub OAuth App yourself** (requires your GitHub account) and supply the Client ID/Secret via configuration when actually running this — I can't create that App on your behalf.
- [x] 1.3 Initial free-tier hypothesis: 200 uploaded runs/month, 1 project, 14-day retention — documented as a hypothesis to validate, not a committed number, per design.md.

## 2. Backend scaffolding

- [x] 2.1 Created `hosted/ReleaseTwin.Hosted.Api` (ASP.NET Core, net10.0) as a new solution/repo area, separate from `ReleaseTwin.sln`
- [x] 2.2 Schema defined via EF Core (`HostedDbContext`: Organization, AppUser, Project, ApiToken, UploadedCaseReport, UploadedFlagProofReport). Runs against Npgsql (Postgres) by connection string or EF InMemory for tests. **Actual cloud Postgres instance provisioning is deferred to deploy time** — needs your own cloud account, same as task 1.1.
- [x] 2.3 Set up CI for the backend (build + test), separate pipeline from the existing CLI/library CI

## 3. Account provisioning (`account-provisioning`)

- [x] 3.1 Implement signup (OAuth and/or magic-link, per task 1.2)
- [x] 3.2 Implement organization and project creation
- [x] 3.3 Implement API token issuance, scoped to a project
- [x] 3.4 Implement API token revocation, with immediate effect on the ingest API
- [x] 3.5 Unit/integration tests for each requirement and scenario in specs/account-provisioning/spec.md

## 4. Ingest API (`ingest-api`)

- [x] 4.1 Define the stable ingest contract (design.md D1) — separate DTOs from `ReleaseTwin.Core`'s `CaseReport`/`FlagProofResult`, containing only metadata fields
- [x] 4.2 Implement API token authentication middleware, rejecting missing/invalid/revoked tokens before any processing
- [x] 4.3 Implement report ingestion: validate against the contract, store scoped to the token's project, reject malformed payloads atomically
- [x] 4.4 Unit/integration tests for each requirement and scenario in specs/ingest-api/spec.md, including an explicit test asserting the contract schema has no field capable of carrying fixture content, response bodies, or credentials

## 5. Dashboard (`dashboard`)

- [x] 5.1 Implement web session authentication (reusing the account-provisioning auth mechanism)
- [x] 5.2 Implement organization-scoped data access (a session can only ever query its own organization's data)
- [x] 5.3 Implement the run history view (case reports, outcome, classification)
- [x] 5.4 Implement the flag-proof results view, distinct from ordinary case results
- [x] 5.5 Unit/integration tests for each requirement and scenario in specs/dashboard/spec.md

## 6. CLI upload integration (`cli-runner`)

- [x] 6.1 Implement mapping from `CaseReport`/`FlagProofResult` to the ingest contract (task 4.1)
- [x] 6.2 Implement the optional upload step in `CliRunner`, gated on an API token environment variable
- [x] 6.3 Implement upload-failure-as-warning behavior (does not affect case outcome or exit code)
- [x] 6.4 Unit tests for each requirement and scenario in specs/cli-runner/spec.md (this change's delta)
- [x] 6.5 Confirm the CLI remains fully functional with zero upload configuration (no regression to Phase 3/4 behavior)

## 7. End-to-end verification

- [x] 7.1 Real cross-process walkthrough performed: started the actual hosted API (SQLite-backed, real process), seeded signup+project+token via a dev-only endpoint that calls the exact same ProvisioningService a real GitHub OAuth callback would (no registered GitHub OAuth App exists yet — see task 1.2 — so the literal browser login step isn't exercised, everything downstream of it is), then ran the real `ReleaseTwin.Cli` as a separate OS process against the live jsonplaceholder API with the issued token. Confirmed via direct SQLite query that the uploaded report landed, correctly scoped to the exact project ID issued at signup. Dashboard rendering of this same data is covered by the 21 passing DashboardModel/ingest tests (real EF Core queries, real scoping logic) rather than a live browser session, for the same GitHub-App reason.
- [x] 7.2 Verified directly (not just assumed from the code) via real EF Core queries against real data in multiple tests: `DashboardModelTests.CustomerSeesOnlyTheirOwnOrganizationsProjects`, `RequestingAnotherOrgsProjectDoesNotSelectIt`, `CannotIssueTokenForAnotherOrganizationsProject` (dashboard side); `IngestApiTests.ReportIsAttributedToTheCorrectProject` and `TokenIsScopedToItsOwnProject` (ingest side). Each creates two real organizations/projects/tokens and asserts the second cannot read or write the first's data.

## 8. Change closeout

- [x] 8.1 Update docs/installation-model.md: the hosted control plane described there as a future possibility now exists (Stage 1, free-only); update its status
- [x] 8.2 Update docs/customer-pilot-guide.md: self-serve signup is now a real onboarding path, not just CLI + manual setup
- [x] 8.3 Update README.md with the self-serve signup flow and a note that billing does not exist yet (Stage 1 is free-only)
- [x] 8.4 Confirm zero changes were needed in `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, or any adapter
- [x] 8.5 Run `openspec validate hosted-self-serve-platform --strict` and resolve any findings
