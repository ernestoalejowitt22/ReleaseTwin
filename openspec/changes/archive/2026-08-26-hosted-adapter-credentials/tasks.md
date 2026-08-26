## 1. Hosted entity, encryption, and storage

- [x] 1.1 New `AdapterCredential` entity (`ProjectId`, `Adapter`, encrypted field-value blob,
      `LastSetByUserId`, `LastSetByDisplayName`, `CreatedAt`, `UpdatedAt`) and `IAdapterCredentialRepository`
      (`SetAsync` upserts in place, `GetAsync`, `DeleteAsync`, `ListByProjectAsync` for metadata-only
      listing) — same single-table-per-project-partition shape as `Connection`, no new GSI.
- [x] 1.2 Wire ASP.NET Core Data Protection with a dedicated purpose string
      (`"ReleaseTwin.AdapterCredentials.v1"`), distinct from `ConnectionStateService`'s own protector
      instance; encrypt the field-value dictionary as one JSON blob before storage, decrypt on read
      (`AdapterCredentialService`).
- [x] 1.3 Per-adapter field manifests declaring required field names, used to validate a dashboard
      submission is complete. Implemented as `AdapterCredentialFieldManifests` inside the hosted API
      itself (not literally `AzureDevOpsAdapter.CredentialFields`/`LaunchDarklyAdapter.CredentialFields`
      as originally sketched — the hosted API has no project reference to the CLI-side adapter
      projects, matching the established "separate solutions/deployments, no shared compiled type"
      convention `IngestCaseReportRequest`/`JourneyVersionResponse` already follow). The CLI side
      (task 4.2) declares its own independent copy of the same field names.
- [x] 1.4 Production Data Protection key persistence: `Amazon.AspNetCore.DataProtection.SSM`'s
      `PersistKeysToAWSSystemsManager` for the real-AWS path in `Program.cs` (guarded by the same
      `useRealDynamoDb` check `TableProvisioning`'s branching already uses), with local/dev/test runs
      continuing to use default ephemeral behavior. Corrected from the original design text's
      `PersistKeysToAwsS3`/`ProtectKeysWithAwsKms` — no such extension methods exist in Microsoft's
      own Data Protection packages; SSM's SecureString parameters are KMS-encrypted at rest natively,
      achieving the same "persisted + KMS-protected" outcome via a real, existing package. Documented
      the required IAM permission in design.md's Migration Plan, same footing as the DynamoDB table's
      own documented manual setup.

## 2. Dashboard-facing endpoints (ClerkJwt)

- [x] 2.1 `PUT /api/adapter-credentials/{projectId}/{adapter}` — set or rotate; validates the
      submitted field set against that adapter's manifest, org/project-scoped like every other
      dashboard endpoint, records `LastSetByUserId`/`LastSetByDisplayName` from the authenticated
      user's claims.
- [x] 2.2 `GET /api/adapter-credentials/{projectId}` — lists configured adapters for a project with
      metadata only (adapter name, last-set-by, last-set-at) — never field values.
- [x] 2.3 `DELETE /api/adapter-credentials/{projectId}/{adapter}` — revoke; a subsequent CLI fetch
      for that project+adapter returns "not configured".

## 3. CLI-facing fetch endpoint (ApiToken)

- [x] 3.1 `GET /api/cli/adapter-credentials/{adapter}` — scoped entirely by the authenticated
      token's `project_id` claim (no separate resource ID to scope by, per design.md's "structurally
      impossible to leak cross-project" decision). Returns the decrypted field dictionary, or a
      distinct "not configured" response (not the same shape as an auth failure) when nothing is
      stored for that project+adapter.

## 4. CLI integration

- [x] 4.1 New `AdapterCredentialsClient` (mirrors `JourneyFetchClient`'s shape: constructor
      `(baseUrl, apiToken, handler?)`, one fetch method, independently-defined response DTO) fetching
      a named adapter's field dictionary.
- [x] 4.2 `CliRunner`: for each credentialed adapter, when its environment variables are entirely
      unset and a project API token is configured, attempt the hosted fetch before deciding the
      adapter is not installed; map the fetched field dictionary onto the same
      `AzureDevOpsOptions`/`LaunchDarklyOptions` constructors the environment-variable path already
      uses. Partial environment configuration remains a startup error exactly as today, independent
      of whether a hosted credential also exists.
- [x] 4.3 Environment-variable precedence: when an adapter's full environment configuration is
      present, skip the hosted fetch entirely (no network call, not just an ignored result).

## 5. Dashboard UI

- [x] 5.1 A project settings section (`web/src/app/dashboard/adapter-credential-form.tsx`, wired into
      `dashboard/page.tsx`) listing each known adapter (Azure DevOps, LaunchDarkly) with its
      configured-or-not status and last-set metadata, each with its own labeled form (not a generic
      key/value editor, per design.md) to set/rotate its fields.
- [x] 5.2 Revoke control per configured adapter; submitted values are never redisplayed after
      saving — fields always render blank/placeholder ("•••••• (leave to overwrite)"), matching the
      API token issuance flow's existing "shown once" convention for the submission moment itself.

## 6. Tests

- [x] 6.1 Hosted: repository/service-level tests for set/rotate/revoke, encryption round-trip, and
      manifest-completeness validation (service-level pattern, matching `JourneyServiceTests`/
      `ConnectionFlowTests`, not an HTTP-level ClerkJwt test — no precedent for faking a real Clerk
      JWT in this codebase).
- [x] 6.2 Hosted: HTTP-level tests for the CLI fetch endpoint (matching `JourneyFetchApiTests`'
      pattern) — valid token fetches its project's credentials; missing token is unauthorized; a
      project with nothing configured gets a distinct "not configured" response, not an error.
- [x] 6.3 CLI: `CliRunnerAdapterCredentialTests` — full env config skips the fetch; fully-absent env
      config with a configured project falls back to the hosted fetch and installs the adapter;
      partial env config is still a startup error even when a hosted credential also exists; full
      env config takes precedence over a different hosted-stored credential; neither source present
      leaves the adapter uninstalled without error. Caught and fixed a real bug while writing these:
      a malformed/incomplete fetched field dictionary crashed `CliRunner` with an unhandled
      `KeyNotFoundException` instead of degrading gracefully — now caught and reported as a `WARN`,
      same as any other optional-adapter resolution failure.

## 7. Real verification

- [x] 7.1 Real, not simulated, verification of the dashboard mechanics: a new Cypress spec
      (`web/cypress/e2e/adapter-credentials.cy.ts`) signs in via real Clerk auth, creates a real
      project, sets LaunchDarkly credentials through the real dashboard form, confirms — across a
      real page reload — that the metadata persists ("Configured by ... on ..."), that the fields
      never redisplay the stored value, that rotation replaces it, and that revoking returns the
      adapter to "Not configured". `cypress run` passed green with real screenshots captured.
      The CLI-side hosted-fetch half of the round trip is already verified for real over a real
      ASP.NET Core test server in `AdapterCredentialFetchApiTests` (task 6.2) and exercised in
      `CliRunnerAdapterCredentialTests` (task 6.3) against a fake handler shaped exactly like that
      real endpoint. Completing the loop against **real** LaunchDarkly/Azure DevOps credentials
      hits the same wall as `chained-journeys`' task 3.5 — no real third-party credentials are
      available in this environment; that gap is not specific to this change.
