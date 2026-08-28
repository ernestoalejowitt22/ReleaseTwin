## 1. Hosted entity and encryption

- [x] 1.1 New `ProjectSecret` entity (`ProjectId`, `SecretName`, encrypted value blob,
      `LastSetByUserId`, `LastSetByDisplayName`, `CreatedAt`, `UpdatedAt`) and
      `IProjectSecretRepository` (`SetAsync` upserts in place, `GetAsync` by name,
      `GetAllAsync` returns every secret for a project, `DeleteAsync`, `ListMetadataAsync` for the
      dashboard's name/metadata-only listing) — one item per (ProjectId, SecretName), same
      single-table-per-project-partition shape as `AdapterCredential`, no new GSI.
- [x] 1.2 Wire ASP.NET Core Data Protection with a dedicated purpose string
      (`"ProjectSecrets.v1"`), distinct from `AdapterCredentials.v1` and `ConnectionStateService`'s
      own protector instance; encrypt each secret's value before storage, decrypt on read
      (`ProjectSecretService`). Reuses the existing SSM-persisted key ring — no new Data Protection
      wiring or deployment step needed.

## 2. Dashboard-facing endpoints (ClerkJwt)

- [x] 2.1 `PUT /api/project-secrets/{projectId}/{name}` — set or rotate; org/project-scoped like
      every other dashboard endpoint, records `LastSetByUserId`/`LastSetByDisplayName`. Denies with
      a distinct `paid-tier-required` error (not a generic failure) when the organization is on the
      Free tier, per `plan-tier-gating`'s existing error-code convention.
- [x] 2.2 `GET /api/project-secrets/{projectId}` — lists a project's stored secret names with
      metadata only (name, last-set-by, last-set-at) — never values.
- [x] 2.3 `DELETE /api/project-secrets/{projectId}/{name}` — revoke; a subsequent CLI fetch no
      longer includes that name.

## 3. CLI-facing fetch endpoint (ApiToken)

- [x] 3.1 `GET /api/cli/project-secrets` — scoped entirely by the authenticated token's `project_id`
      claim (no separate resource ID to scope by, matching `adapter-credentials`' own
      structurally-impossible-to-leak-cross-project shape). Returns every decrypted name/value pair
      for that project, or an empty set when none are stored — a distinct, clear outcome from an
      auth failure.

## 4. CLI integration

- [x] 4.1 New `ProjectSecretsClient` (mirrors `AdapterCredentialsClient`'s shape: constructor
      `(baseUrl, apiToken, handler?)`, one fetch method, independently-defined response DTO)
      fetching a project's full secret set.
- [x] 4.2 `CaseFileLoader`: add an injectable environment-resolution seam (defaulting to today's
      live `Environment.GetEnvironmentVariable` behavior when not supplied) so `${VAR_NAME}`
      resolution can be driven by a caller-supplied lookup instead of always reading the real
      process environment directly.
- [x] 4.3 `CliRunner`: when a project API token is configured, fetch that project's secrets once
      per run and build an effective environment lookup (local process environment first, the
      fetched secrets as fallback for names the local environment doesn't have); pass that lookup
      into the `CaseFileLoader` seam from 4.2. No project token configured: behavior is unchanged
      (today's direct live-environment resolution).
- [x] 4.4 Fetch failure (network error, no token, nothing stored) degrades gracefully — an
      unresolvable `${VAR_NAME}` reference still produces case-loading's existing clear load-time
      error, never a crash or a silently-substituted blank value.

## 5. Dashboard UI

- [x] 5.1 A project settings section (alongside the existing per-adapter credential forms) listing
      a project's stored secrets by name with last-set metadata, plus an "add a secret" control
      (name + value fields, not a fixed manifest) to set new ones.
- [x] 5.2 Rotate and revoke controls per stored secret; submitted values are never redisplayed after
      saving, matching `adapter-credentials`' existing convention.
- [x] 5.3 Free-tier organizations see the section in a locked/upgrade-prompting state (consistent
      with `plan-tier-gating`'s existing Free-tier UI treatment elsewhere on the dashboard) rather
      than a bare error only surfacing after a failed submit.

## 6. Tests

- [x] 6.1 Hosted: repository/service-level tests for set/rotate/revoke, encryption round-trip, and
      the Paid-tier gate (service-level pattern, matching `AdapterCredentialServiceTests`).
- [x] 6.2 Hosted: HTTP-level tests for the CLI fetch endpoint (matching
      `AdapterCredentialFetchApiTests`'s pattern) — valid token fetches its project's secrets;
      missing token is unauthorized; a project with nothing configured gets an empty set, not an
      error; a wrong-project token cannot fetch another project's secrets.
- [x] 6.3 CLI: `CaseFileLoaderEnvironmentSeamTests` — the injectable lookup resolves a case file's
      `${VAR_NAME}` reference when supplied; omitting it preserves today's exact live-environment
      behavior (regression coverage for 4.2's seam).
- [x] 6.4 CLI: `CliRunnerProjectSecretsTests` — a local environment variable takes precedence over a
      same-named hosted secret with no fetch attempted; a hosted secret resolves a reference the
      local environment lacks; neither source present still produces case-loading's existing
      missing-reference error; a fetch failure degrades to the same missing-reference error rather
      than crashing.

## 7. Real verification

- [x] 7.1 A new Cypress spec signing in via real Clerk auth, creating a real project, setting a
      project secret through the real dashboard form, confirming — across a real page reload — that
      the metadata persists and the value never redisplays, that rotation replaces it, and that
      revoking removes it.
- [x] 7.2 A real, end-to-end run: a case file referencing `${SOME_SECRET_NAME}` with no matching
      local environment variable, run via the real CLI against a project with that secret set
      through the real dashboard — confirms the hosted-fetch fallback resolves it for real, not
      just against a fake handler.
