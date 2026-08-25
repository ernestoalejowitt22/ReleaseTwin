## Context

See `proposal.md` - Why. Every access pattern below is derived from the actual current code, not guessed:

- `ProvisioningService.GetOrCreateUserAsync` (`Services/ProvisioningService.cs`): looks up `AppUser` by `ClerkUserId`; on miss, creates `Organization` + `AppUser` together in one transaction (get-or-create, race-prone under concurrent first logins for the same Clerk user).
- `ProvisioningService.CreateProjectAsync`: inserts `Project` by `OrganizationId`.
- `ProvisioningService.IssueTokenAsync`/`RevokeTokenAsync`: inserts `ApiToken` by `ProjectId`; revokes by `ApiToken.Id`.
- `ApiTokenAuthenticationHandler`: looks up `ApiToken` by `TokenHash` on **every ingest request** — the account-provisioning spec's "a revoked token is rejected immediately" scenario means this read cannot tolerate replication lag.
- `IngestEndpoints`: inserts `UploadedCaseReport`/`UploadedFlagProofReport` by `ProjectId`, using only the `project_id` claim from the authenticated token — no `Organization` context is available here today.
- `DashboardService.GetDashboardViewAsync`: lists `Project`s by `OrganizationId` ordered by name; resolves one `Connection` by `ProjectId`; lists `ApiToken`s by `ProjectId` ordered by `CreatedAt` desc; lists `UploadedCaseReport`/`UploadedFlagProofReport` by `ProjectId` ordered by `UploadedAt` desc.
- `ConnectionEndpoints`: creates/deletes one `Connection` per `ProjectId` (1:1).
- No production data exists anywhere (Stage 1, pre-pilot) — this is a clean cutover, not a live migration with a backfill/rollback concern for real customer data.

## Goals / Non-Goals

**Goals:**
- Every existing access pattern above is served with equal or better correctness (especially the strong-consistency requirement on token auth) on DynamoDB.
- Usage counts are read in O(1) — a single `GetItem`, never a fan-out scan/query across an organization's projects.
- No existing spec-level observable behavior changes (account-provisioning, ingest-api, project-connections, and the pre-existing dashboard requirements all continue to hold).
- Tests stay fast: unit tests run against in-memory fakes, not real AWS or DynamoDB Local, mirroring the existing EF Core in-memory-provider role.

**Non-Goals:**
- Live/dual-write migration tooling, backfill scripts, or a rollback-to-EF-Core path. Not needed: there is no production data.
- Any pricing/entitlement/gating logic — unchanged from the original usage-metering scope.

## Decisions

### Table design: single table, per explicit decision

One physical table, `ReleaseTwinHosted`, hosts every entity — generic `PK`/`SK` string attributes plus an `EntityType` discriminator, the standard DynamoDB single-table pattern for a bounded, known set of access patterns (which is exactly what section Context above enumerates; this isn't ad-hoc query flexibility). "Single table" does **not** mean every item shares one partition key value — four entities deliberately keep their own top-level partition instead of nesting under their parent organization, each for a specific access-pattern reason called out below. That's normal single-table practice: one physical table hosting several distinct item-key "shapes," not one shape for everything.

| Entity | PK | SK | Why this key |
|---|---|---|---|
| Organization | `ORG#<orgId>` | `ORG#<orgId>` | Root item for the org's partition. |
| Project | `ORG#<orgId>` | `PROJECT#<projectId>` | "List projects by org" (the dominant read) is `Query(PK=ORG#<orgId>, SK begins_with "PROJECT#")` — no GSI needed. "Resolve/authorize one project within an org" (also what `ConnectionEndpoints`' `ProjectBelongsToOrganizationAsync` needs) is a direct `GetItem` on the full key. |
| UsageCounter (new) | `ORG#<orgId>` | `COUNTER#<period>` | One low-volume item per org per calendar month (e.g. `COUNTER#2026-08`) in the org partition — an aggregate *about* the org, not a growing per-report collection, so no hot-partition concern. |
| **Connection** (own top-level partition) | `CONN#<projectId>` | `CONN#<projectId>` | Every call site (`ConnectionService`, `DashboardService`) already looks it up by `ProjectId` alone — there is no "list connections by org" access pattern anywhere in the code. Nesting it under `ORG#<orgId>` would mean threading `OrganizationId` through call sites for no real benefit; kept off the org partition for the same "the natural key is what's actually used" reasoning already applied to `ApiToken` and reports below. |
| **AppUser** (own top-level partition) | `USER#<clerkUserId>` | `USER#<clerkUserId>` | The dominant — only — lookup pattern is by `ClerkUserId`, and get-or-create needs a strongly-consistent uniqueness guarantee (`attribute_not_exists`) that only a table's own primary key gives natively. Nesting under `ORG#` would lose that; "list users by org" has no current caller, so it isn't built. |
| **ApiToken** (own top-level partition) | `TOKEN#<tokenHash>` | `TOKEN#<tokenHash>` | The hot-path lookup (`ApiTokenAuthenticationHandler`, every ingest request) needs strong consistency — DynamoDB GSIs are only *eventually* consistent, which would put "a revoked token is rejected immediately" at risk. Attrs include the denormalized `OrganizationId` (see below). |
| **UploadedCaseReport** / **UploadedFlagProofReport** (own top-level partition) | `PROJECT#<projectId>` | `CASEREPORT#<uploadedAt>#<id>` / `FLAGPROOF#<uploadedAt>#<id>` | Kept off the `ORG#` partition deliberately: report volume grows per project over time and is the highest-write-frequency item type in the whole schema. Nesting it under `ORG#<orgId>` would make every project in a busy organization write into the *same* partition, risking a hot-partition throughput ceiling as usage grows — exactly the anti-pattern single-table design warns against. No current read pattern needs "reports across an org in one query" anyway (that's what the `UsageCounter` aggregate is for). |

GSIs — only `ApiToken` needs one, since it's the only entity whose primary key (by design) isn't reachable from its parent hierarchy:
- **GSI1** (`GSI1PK`, `GSI1SK`): `GSI1PK = PROJECT#<projectId>`, `GSI1SK = TOKEN#<createdAt>#<id>` — serves the dashboard's "list tokens by project" ordered by creation time. Eventually consistent, which is fine — it's a UI listing, not the auth check.
- **GSI2** (`GSI2PK`, `GSI2SK`): `GSI2PK = TOKENID#<id>`, `GSI2SK = TOKENID#<id>` — serves `RevokeTokenAsync(tokenId)`'s existing by-id signature: `Query GSI2` to find the `TokenHash`, then `UpdateItem` on the primary table. Low-frequency admin action; eventual consistency here is an acceptable trade for not needing a third top-level partition just for this one operation.

### Usage counters: written atomically at ingest time, not computed at read time

A relational `COUNT() ... JOIN Project ON ... WHERE OrganizationId = X` has no efficient DynamoDB equivalent — there's no server-side join, and scanning every project in an org at dashboard-load time to sum their report counts would be slow and get worse as an org grows. This holds regardless of single- vs. multi-table design; it's a property of DynamoDB itself, not the table layout. Instead, `IngestEndpoints` performs an `UpdateItem` with `ADD CaseReportCount :one` (or `FlagProofReportCount`) against the `UsageCounter` item at `(PK=ORG#<orgId>, SK=COUNTER#<current period>)` **in the same request that stores the report**, using DynamoDB's native atomic-counter support (`ADD` is a true atomic increment, safe under concurrent ingest requests — no read-modify-write race). The dashboard then reads usage with a single `GetItem` — O(1), no query, no join, no fan-out.

This requires `OrganizationId` to be available at ingest time, which it isn't today (only `project_id` is a claim on the authenticated principal). **Decision: denormalize `OrganizationId` onto the `ApiToken` item itself at issuance time** (`ProvisioningService.IssueTokenAsync` reads the token's `Project.OrganizationId` once, at issuance, and stores it alongside `ProjectId`). `ApiTokenAuthenticationHandler` then emits a second claim, `organization_id`, alongside the existing `project_id` claim — so `IngestEndpoints` never needs an extra read to find out which org's counter to increment. This is standard NoSQL denormalization (trade normalization for read/write efficiency on a hot path) and carries no staleness risk in practice: nothing in this codebase ever transfers a `Project` between organizations.

### Cross-entity transactions

`GetOrCreateUserAsync`'s "create `Organization` + `AppUser` together" is currently one EF Core `SaveChangesAsync` (one implicit transaction). On DynamoDB this becomes a single `TransactWriteItems` call against the shared table: `Put Organization` item (unconditional) + `Put AppUser` item with condition `attribute_not_exists(PK)`. If the conditional check fails (a concurrent request already created the user — the race this get-or-create pattern exists to guard against), the caller re-reads via `GetItem` instead of retrying the create, exactly mirroring the current EF Core code's "check existing, then create" shape but now race-safe by construction rather than by luck.

### Repository abstraction, mirroring the current per-entity `DbSet<T>` shape

One interface per entity (`IOrganizationRepository`, `IUserRepository`, `IProjectRepository`, `IApiTokenRepository`, `IConnectionRepository`, `ICaseReportRepository`, `IFlagProofReportRepository`, `IUsageCounterRepository`), each with a real DynamoDB implementation (all sharing the one `ReleaseTwinHosted` table via the low-level `AmazonDynamoDBClient`) and an in-memory fake for unit tests — this directly replaces `HostedDbContext`'s role, including the "tests run offline/fast" property the existing 31-test suite already relies on. `ProvisioningService`, `DashboardService`, `ApiTokenAuthenticationHandler`, `IngestEndpoints`, and `ConnectionEndpoints` take these interfaces via DI instead of `HostedDbContext`.

**SDK usage: low-level `AmazonDynamoDBClient` throughout, not the higher-level `DynamoDBContext`.** `DynamoDBContext`'s object-persistence model (one POCO type mapped to one table, attribute-name-based) doesn't fit a single table whose `PK`/`SK` are deliberately generic and overloaded across entity types with an `EntityType` discriminator — that mapping is idiomatically hand-written (build/parse the `Dictionary<string, AttributeValue>` per repository), which is also what's needed anyway for `TransactWriteItems`, conditional writes, and atomic `ADD` increments, none of which `DynamoDBContext` exposes cleanly. Each repository owns its own item-shape (de)serialization; a small shared helper (e.g. mapping key prefixes ↔ Guid/string ids) avoids repeating the prefix string-building logic in every repository.

### Local development and testing, replacing the EF Core three-tier story

| Today (EF Core) | Replacement |
|---|---|
| Postgres (production) | Real AWS DynamoDB, credentials via the AWS SDK's default credential chain (environment/shared config/IAM role) — no hardcoded keys, consistent with this project's existing "no hardcoded credentials" rule. |
| SQLite (real, persistent, file-based local stand-in) | DynamoDB Local (official Docker image), run via `docker run`/`docker-compose` — a real, persistent (with a mounted volume), locally-run DynamoDB-compatible server. Configured via `Aws:DynamoDb:ServiceUrl` (e.g. `http://localhost:8000`) overriding the SDK's default AWS endpoint. |
| In-memory EF provider (test fallback) | The new in-memory repository fakes — hand-rolled per interface, since DynamoDB's SDK has no built-in in-memory provider. A smaller set of integration tests run against DynamoDB Local specifically to exercise conditional writes/transactions/atomic increments, which a fake can't meaningfully verify. |

New configuration, mirroring the existing `Database:*`/`ConnectionStrings:*` naming convention: `Aws:Region`, `Aws:DynamoDb:TablePrefix` (e.g. `releasetwin-hosted-dev-`), `Aws:DynamoDb:ServiceUrl` (optional, set only for DynamoDB Local).

## Risks / Trade-offs

- [Denormalized `OrganizationId` on `ApiToken` could go stale if projects were ever reassigned between organizations] → Accepted; no such feature exists or is planned, and adding it would already require touching every token issued under the old organization regardless of this design.
- [Eventually-consistent GSIs (`GSI1`/`GSI2` on tokens) mean the dashboard could show a just-issued token with a few hundred milliseconds' delay before it appears in the by-project listing] → Accepted; these are UI listings, not security checks — the one place strong consistency is load-bearing (token auth) deliberately uses the table's own primary key, not a GSI.
- [A single shared table is harder to reason about at a glance than one table per entity — every repository must agree on the generic `PK`/`SK`/`EntityType` conventions rather than each having its own natural columns] → Accepted per explicit decision; mitigated by keeping the per-entity key scheme documented in one place (the table above) and giving each entity type disjoint key prefixes so items never collide across types.
- [Report items (`PROJECT#<projectId>` partition) were deliberately kept off the org-wide partition to avoid a hot-partition risk as report volume grows — this means there is genuinely no single-query way to list "every report across an org," should that ever be needed for something other than the count] → Accepted; `UsageCounter` already serves the aggregate-count need, and no current access pattern needs a raw cross-project report listing. If one arises later, it's an additive GSI, not a redesign.
- [Hand-rolled in-memory fakes must stay behaviorally faithful to real DynamoDB semantics (e.g. conditional-write failure modes) or tests could pass against a fake while the real implementation is wrong] → Mitigated by the integration-test tier against real DynamoDB Local specifically for the conditional/transactional/atomic-counter paths, not relying on fakes alone for those.
- [This is a full rewrite of every hosted-API persistence path in one change] → Accepted per explicit decision; safe specifically because there is no production data to protect during the cutover (pre-pilot, Stage 1).
- [AWS becomes a new operational dependency (region choice, IAM setup, table provisioning) where none existed before] → Inherent to the explicit "deploying on AWS" motivation for this change; not a cost specific to this design.

## Migration Plan

No live migration is needed — there is no production data. Steps are purely additive/replacement, not a phased rollout with a live cutover moment:

1. Stand up AWS SDK scaffolding, the single-table definition (with its two GSIs, provisioned via Terraform for real AWS — per explicit decision — plus local auto-provisioning against DynamoDB Local only), and DynamoDB Local for local dev — buildable and testable independent of removing EF Core.
2. Implement each repository interface (DynamoDB + in-memory fake) one entity at a time, with its own tests, alongside the still-functioning EF Core code (both can coexist temporarily during implementation).
3. Switch each service (`ProvisioningService`, `DashboardService`, `ApiTokenAuthenticationHandler`, `IngestEndpoints`, `ConnectionEndpoints`) to the new repositories, one at a time, re-running its existing tests against the new backing store.
4. Implement `UsageCounters` + the ingest-time atomic increment + the dashboard's `GetItem` read.
5. Remove `HostedDbContext`, the EF Core entity classes' persistence role, and the `Microsoft.EntityFrameworkCore.Sqlite`/`Npgsql.EntityFrameworkCore.PostgreSQL` package references once nothing depends on them.

**Rollback**: since there's no production data, rollback is simply not merging/deploying this change — no data-recovery concern exists.

## Open Questions

- Exact AWS region and table-naming/prefix convention for the real production deployment — deferrable, doesn't change the design (config value, not a structural decision) — resolve when actual AWS account/environment details are available.
