## Why

The hosted platform stores every uploaded case/flag-proof report scoped to an `Organization`, but nothing counts or aggregates them — the dashboard lists full report rows and nothing else. Before any pricing or entitlement decision can be made, we need to know what usage-based billing would even measure. Counting is pure observability: it requires no pricing decision, no gating, and no Stripe integration.

**Scope expanded per explicit decision**: the hosted platform is moving to AWS as its deployment target, and DynamoDB is the chosen data store going forward. Since usage counts need `Organization`/`Project` data, and that data currently lives entirely in EF Core (Postgres/SQLite/in-memory via `HostedDbContext`), usage-metering cannot be built against a different database than everything else it reads. This change therefore also migrates the entire hosted data model off EF Core onto DynamoDB, with usage-metering's counters built natively on top of that new store rather than translated from a relational design. There is no production customer data to migrate — the hosted platform remains pre-pilot (Stage 1, not yet offered to anyone outside this repo per `docs/installation-model.md`) — so this is a clean cutover, not a live migration.

## What Changes

- Replace `HostedDbContext` (EF Core: Postgres prod / SQLite local / in-memory tests) with DynamoDB-backed repositories for every entity: `Organization`, `AppUser`, `Project`, `ApiToken`, `Connection`, `UploadedCaseReport`, `UploadedFlagProofReport`.
- Add AWS SDK scaffolding: a single DynamoDB table (single-table design, per explicit decision — see design.md) hosting every entity, region/credential configuration (via the standard AWS default credential chain — no hardcoded keys), and a local-dev story using DynamoDB Local as the real, persistent, file-based-equivalent local stand-in (parallel role to SQLite today).
- Add a new `UsageCounter` item type in that same table: an atomically-incremented counter per `(OrganizationId, calendar month)`, updated at ingest time rather than computed by a fan-out read-time aggregation query (the idiomatic DynamoDB pattern for this kind of metric — a relational `COUNT() ... JOIN` has no efficient DynamoDB equivalent).
- Surface "N runs this month" in the dashboard, scoped to the customer's own organization, read via a single `GetItem` against that counter item.
- Preserve every existing observable behavior contract (no MODIFIED requirements to `account-provisioning`, `ingest-api`, `project-connections`, or the existing `dashboard` requirements) — this is an implementation swap, not a behavior change, except where usage-metering itself adds new behavior.

## Capabilities

### New Capabilities
- `usage-metering`: counting and periodizing uploaded report volume per organization, as the foundation for any future usage-based pricing. (Unchanged from the original proposal — still describes observable behavior only, not the DynamoDB implementation.)

### Modified Capabilities
- `dashboard`: adds a usage summary (report counts for the current period) to the dashboard view. (Unchanged from the original proposal.)

## Impact

- **Removed**: `HostedDbContext`, all EF Core entity classes' role as the persistence model, `Microsoft.EntityFrameworkCore.Sqlite`/`Npgsql.EntityFrameworkCore.PostgreSQL` package dependencies, the three-tier Postgres/SQLite/in-memory selection in `Program.cs:18-34`.
- **Added**: `AWSSDK.DynamoDBv2` dependency; per-entity repository interfaces + DynamoDB implementations, all sharing one physical table; an in-memory fake implementation per repository for fast unit tests (replacing EF Core's in-memory provider); DynamoDB Local for local/integration runs; a new `IUsageCounterRepository` and item type; AWS region/table-prefix/local-endpoint configuration.
- **Affected services**: `ProvisioningService`, `DashboardService`, `ApiTokenAuthenticationHandler`, `IngestEndpoints`, `ConnectionEndpoints` — every place that currently depends on `HostedDbContext` moves to the new repository interfaces.
- **Test suite**: the hosted API's 31 tests move from EF Core's in-memory provider to the new repository fakes; a smaller set of integration tests run against real DynamoDB Local to exercise conditional writes/transactions/atomic counters that fakes can't meaningfully verify.
- **No changes** to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, any adapter, the CLI, or `web/`'s API contract shape (the JSON the frontend consumes is unchanged; only what's behind `HostedDbContext` today changes).
