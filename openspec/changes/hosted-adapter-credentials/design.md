## Context

See proposal.md for why. Relevant existing facts:
- `hosted/ReleaseTwin.Hosted.Api` has no EF Core / relational database — a single-table DynamoDB
  design (`IHostedTable`, `Attrs.Keys`, hand-written entity-to-item mapping per repository). Every
  new entity so far (`Project`, `ApiToken`, `Connection`, `Journey`/`JourneyVersion`) follows that
  same recipe: new key prefixes, a plain POCO entity, a repository against `IHostedTable`.
- Two auth schemes already exist and are used exactly for the two directions this capability needs:
  `"ClerkJwt"` for web-session/dashboard endpoints (`DashboardEndpoints`, `JourneyEndpoints`), and
  `ApiTokenDefaults.Scheme` for CLI-facing endpoints (`IngestEndpoints`, `JourneyFetchEndpoints`) —
  the latter already resolves a request's project via `ApiTokenDefaults.ProjectIdClaim` off the
  authenticated token, the exact mechanism this capability's CLI fetch endpoint reuses.
- `ConnectionStateService` already uses ASP.NET Core's Data Protection API
  (`IDataProtectionProvider`/`IDataProtector`) to encrypt a short-lived OAuth state value. This is
  the only encryption primitive already in use anywhere in this codebase.
- `CliRunner.cs` currently resolves each credentialed adapter's config from a fixed list of
  environment variable names (`AzureDevOpsEnvironmentVariables`, `LaunchDarklyEnvironmentVariables`),
  each checked as an all-or-nothing group: any subset present is a startup error, none present means
  the adapter is simply not installed (not an error, since installing it is optional).
- `chained-journeys` already established the "CLI fetches something from the hosted API using its
  project token" pattern (`JourneyFetchClient`, `--journey <id>@<version>`) — this capability adds a
  second, structurally similar fetch (adapter credentials instead of pipeline YAML), reusing the same
  trust boundary rather than inventing a new one.

## Goals / Non-Goals

**Goals:**
- A customer can set, rotate, and revoke an adapter's execution credentials for a project through
  the dashboard, without touching environment variables.
- The CLI transparently prefers environment variables when present (so nothing changes for a
  customer who never adopts this) and falls back to a hosted fetch otherwise.
- Adding a third or fourth adapter's credential fields to this mechanism costs a field-name list, not
  a new endpoint, entity, or encryption path.
- Stored secret values are encrypted at rest and never redisplayed by the dashboard once set.

**Non-Goals:**
- Not building a general-purpose secrets manager — this is scoped to the specific field sets the
  Azure DevOps and LaunchDarkly adapters already declare today. A future adapter's credentials reuse
  the same mechanism by declaring its own field list, not by generalizing further than that now.
- Not removing or deprecating the environment-variable path. It remains fully supported and takes
  precedence — this is additive.
- Not solving multi-region or multi-account key management beyond what's needed for this codebase's
  existing single-account AWS deployment.
- Not building an audit log of who viewed/changed credentials beyond the existing "last set by /
  last set at" metadata already required by the spec — a fuller audit trail is a candidate follow-up,
  not required here.

## Decisions

**Encrypt using ASP.NET Core Data Protection (`IDataProtector`), not a hand-rolled crypto scheme.**
It's already a working, tested primitive in this exact codebase (`ConnectionStateService`), needs no
new package, and gets key-rotation semantics (multiple keys in a ring, old ciphertext still
decryptable after rotation) for free. A distinct purpose string (`"AdapterCredentials.v1"`) isolates
this from `ConnectionStateService`'s own protector instance so the two can never cross-decrypt each
other's payloads. Alternative considered: `AWSSDK.KeyManagementService` directly (envelope
encryption via KMS) — more standard for "real" secrets management, but a bigger lift (a new AWS
dependency, explicit key ARN configuration, no existing precedent in this codebase) for what Data
Protection already covers adequately at this stage; revisit if a compliance requirement demands KMS
specifically.

**Data Protection's key ring must be persisted and protected explicitly for this to be safe in
production — this is the real risk in this design, not an afterthought.** By default, ASP.NET Core
persists Data Protection keys to the local filesystem, which does not survive a redeploy or work
across multiple instances — losing the key ring makes every stored credential permanently
undecryptable, and an unprotected, non-KMS-wrapped key ring stored inside the AWS account is a real
exposure if that storage location is ever over-permissioned. This needs the same treatment as the
DynamoDB table itself already gets (`TableProvisioning`'s documented manual setup, never
auto-provisioned against real AWS): persist the key ring to AWS Systems Manager Parameter Store as a
SecureString (`Amazon.AspNetCore.DataProtection.SSM`'s `PersistKeysToAWSSystemsManager`) — SSM
encrypts SecureString parameters via KMS itself, so this gets the "persisted + KMS-protected" outcome
without hand-implementing a custom `IXmlRepository`/`IXmlEncryptor` pair for S3 (no built-in
`PersistKeysToAwsS3`/`ProtectKeysWithAwsKms` extension exists in Microsoft's own Data Protection
packages — corrected during implementation from this design's original, inaccurate naming). Local/
test runs continue using the default ephemeral/filesystem behavior exactly as `ConnectionStateService`'s
own tests already do via `EphemeralDataProtectionProvider`. This is a concrete task (1.4 in
tasks.md), not an implicit assumption.

**One entity, `AdapterCredential`, keyed by (ProjectId, Adapter) as a single item, not one entity per
field.** Mirrors `Connection`'s existing shape (one item per project, upsert-in-place — see
`ConnectionRepository.UpsertAsync`) rather than `ApiToken`/`JourneyVersion`'s
immutable-append-a-new-row shape, because rotation here is explicitly "replace the value entirely"
(spec: "the previous values are no longer retrievable"), not an append-only history. Fields are
stored as one Data-Protected JSON blob (`Dictionary<string,string>` of field name to value) rather
than one encrypted column per field — simpler mapping code, and per-field granularity buys nothing
here since the whole credential is always set/fetched/rotated together.

**Adapter field names are declared once per adapter, the same way `KnownOperationCapabilities` is
already declared once per adapter today.** A new static manifest (e.g.
`AzureDevOpsAdapter.CredentialFields`) lists the field names a customer must supply (`org`,
`project`, `pat`, `areaPath`, `variableGroupId` for Azure DevOps; `apiToken`, `projectKey`,
`environmentKey` for LaunchDarkly) so the dashboard's set-credential endpoint can validate a
submission is complete without hardcoding per-adapter knowledge into the hosted API's own code, and
`CliRunner` can map a fetched field dictionary back onto the same `AzureDevOpsOptions`/
`LaunchDarklyOptions` constructors it already calls for the environment-variable path. A future
adapter needing this mechanism declares its own list the same way — no endpoint or entity change.

**The CLI-facing fetch endpoint takes no explicit project or credential identifier — only the
adapter name.** `GET /api/cli/adapter-credentials/{adapter}`, scoped entirely by the authenticated
token's `project_id` claim (identical resolution to `IngestEndpoints.GetProjectId`). Unlike
`hosted-journeys`' fetch (which needs a journey ID a token could theoretically be handed for the
wrong project), there is no separate resource ID here for a wrong-project token to probe — the
"cannot fetch another project's credentials" requirement is satisfied structurally by the endpoint's
shape, not by an additional runtime ownership check.

**Environment-variable precedence is resolved once per adapter, before installation, not merged
field-by-field.** If an adapter's full environment configuration is present, the hosted fetch is
never attempted at all (not just ignored after fetching) — avoids an unnecessary network call on the
common path (a customer who already uses env vars sees zero behavior or latency change), and avoids
having to reason about partially-mixed env+hosted field sets for one adapter.

**Dashboard UI: one form per configured-or-configurable adapter on a project's settings, not a
generic key/value editor.** Same reasoning as `chained-journeys`' journey-builder choice of a
generic step editor doesn't apply here — unlike a case's pipeline steps (arbitrarily many, ordered,
adapter-agnostic), there are exactly two known adapters today and their field sets are fixed and
well-known ahead of time, so a small, explicit form per adapter (labeled fields, not raw key/value
rows) is both less code and clearer for the customer than a generic editor would be.

## Risks / Trade-offs

- **Key-ring loss is unrecoverable by design.** → Mitigated by explicit S3+KMS persistence as a
  required deployment task (see Decisions above), not left implicit; documented the same way the
  DynamoDB table's own manual creation already is.
- **A revoked/rotated credential already fetched by a running CLI process stays in memory for that
  process's lifetime.** → Accepted: matches how an environment-variable credential already behaves
  today (a long-running CI job doesn't re-read its own environment mid-run either) — not a new
  category of risk this change introduces.
- **This is the second thing (after journey content) the hosted platform hands the CLI to use
  directly, and the first that's a live credential to a third-party system, not pipeline content.**
  → Explicitly called out in proposal.md, not softened. Mitigated by the same encryption-at-rest +
  TLS-in-transit + project-token-scoping approach as everything else sensitive in this codebase, and
  by keeping the environment-variable path fully supported for anyone who'd rather not extend that
  trust at all.
- **Adding a new adapter's credential fields later requires a coordinated change across the hosted
  API's field manifest, the dashboard form, and `CliRunner`'s field-to-options mapping.** → Accepted
  for this pass (see Non-Goals — a fully adapter-agnostic generic mechanism is more machinery than
  two known adapters justify); revisit if a third or fourth adapter makes the coordination cost
  visible the way LaunchDarkly's arrival made the *original* env-var-only gap visible.

## Migration Plan

No data migration — this is a wholly new entity and new endpoints; nothing existing changes shape.
Two real deployment steps beyond code, analogous to the DynamoDB table's own documented manual setup:
1. Grant the hosted API's IAM role permission to read/write the SSM parameter Data Protection
   persists its key ring to (`PersistKeysToAWSSystemsManager`, real-AWS path only — local/dev
   continues using default ephemeral behavior, matching `ConnectionStateService`'s existing test
   setup).
2. No table schema change needed — `AdapterCredential` fits the existing single-table
   `PK`/`SK`/`GSI1`/`GSI2` schema with no new GSI (see tasks.md for the exact key shape).
