## 1. AWS/DynamoDB scaffolding

- [x] 1.1 Add `AWSSDK.DynamoDBv2` package reference to `ReleaseTwin.Hosted.Api.csproj`.
- [x] 1.2 Add configuration: `Aws:Region`, `Aws:DynamoDb:TablePrefix`, `Aws:DynamoDb:ServiceUrl` (optional, for local override), following the existing `Database:*`/`ConnectionStrings:*` naming convention in `Program.cs`.
- [x] 1.3 Register `AmazonDynamoDBClient` in DI, honoring `ServiceUrl` when set (points at DynamoDB Local) and the AWS SDK's default credential chain otherwise (no hardcoded keys). (Using the low-level client throughout, not `DynamoDBContext` — see design.md's SDK-usage decision.)
- [x] 1.4 Provision the single `ReleaseTwinHosted` table with its `PK`/`SK` and the two GSIs (`GSI1`, `GSI2`) via Terraform (`hosted/terraform/main.tf`, per explicit decision — supersedes the originally-planned shell script, avoiding two provisioning mechanisms that could drift from `TableProvisioning.cs`'s local-dev auto-provisioning). Validated with `terraform init`/`terraform validate`.
- [x] 1.5 Add `hosted/docker-compose.yml` for DynamoDB Local, playing the role SQLite played before this migration.

## 2. Repository interfaces and implementations

- [x] 2.1 Define `IOrganizationRepository`, `IUserRepository`, `IProjectRepository`, `IApiTokenRepository`, `IConnectionRepository`, `ICaseReportRepository`, `IFlagProofReportRepository`, `IUsageCounterRepository`.
- [x] 2.2 Implement each against the single `ReleaseTwinHosted` table per design.md's key design — `Users` via conditional `PutItem` (`PK=USER#<clerkUserId>`, `attribute_not_exists(PK)`); `ApiTokens` via `TOKEN#<tokenHash>` as primary key plus `GSI1` (by-project listing) and `GSI2` (by-id, for revoke); `Projects`/`UsageCounters` nested under `PK=ORG#<orgId>`; `Connections` and `UploadedCaseReports`/`UploadedFlagProofReports` in their own top-level partitions (see design.md's revised Connection key note); `UsageCounters` updates via atomic `ADD` increments. Implemented via a shared `IHostedTable` abstraction (`DynamoDbHostedTable`/`InMemoryHostedTable`) so each repository's mapping logic is written once, not duplicated per backend.
- [x] 2.3 Implement `GetOrCreateUserAsync`'s organization+user creation as a single `TransactWriteItems` call (conditional on the user side), per design.md's Cross-entity transactions decision — `UserRepository.CreateWithOrganizationAsync`.
- [x] 2.4 Implement an in-memory fake for each repository interface, faithful enough to real DynamoDB semantics for unit tests (conditional-write failures, atomic increments) to be meaningful — `InMemoryHostedTable`, shared by all 8 repositories.

## 3. Denormalized organization id on tokens

- [x] 3.1 Add `OrganizationId` to the `ApiToken` model, populated once at issuance in `ProvisioningService.IssueTokenAsync` from the token's project.
- [x] 3.2 Add an `organization_id` claim to the principal `ApiTokenAuthenticationHandler` emits, alongside the existing `project_id` claim.

## 4. Service migration (one at a time, existing tests re-run against each)

- [x] 4.1 Migrate `ProvisioningService` to the new repositories. (`IssueTokenAsync` now takes `organizationId` explicitly — every caller already has it in scope; see the method's own doc comment.)
- [x] 4.2 Migrate `ApiTokenAuthenticationHandler` to `IApiTokenRepository`, verifying the strongly-consistent revoked-token-rejected-immediately behavior still holds (`GetByHashAsync` is a consistent `GetItem` on the primary key, not a GSI).
- [x] 4.3 Migrate `DashboardService` to the new repositories, preserving `GetDashboardViewAsync`'s existing per-project scoping.
- [x] 4.4 Migrate `ConnectionEndpoints`/`ConnectionService`/`DashboardEndpoints` to the new repositories (also removed direct `HostedDbContext` use in `DashboardEndpoints`' token/connection authorization checks).
- [x] 4.5 Migrate `IngestEndpoints` to `ICaseReportRepository`/`IFlagProofReportRepository`, adding the atomic `UsageCounters` increment (keyed by the new `organization_id` claim + current UTC calendar month) in the same request that stores each report.

## 5. Usage-metering feature itself

- [x] 5.1 Add `DashboardUsageSummary(int CaseReportCount, int FlagProofReportCount, DateOnly PeriodStart)` and a field on `DashboardView`.
- [x] 5.2 In `GetDashboardViewAsync`, read the current period's `UsageCounters` item via `IUsageCounterRepository.GetAsync(organizationId, currentPeriod)` — a single `GetItem`, independent of `selectedProject`; treat a missing item as zero, not an error.
- [x] 5.3 Already satisfied by the earlier `token-onboarding` session's read of the Server/Client Components doc — this page is a plain Server Component using only patterns already present in `page.tsx`, nothing novel introduced.
- [x] 5.4 Render the usage summary in `web/src/app/dashboard/page.tsx`, near the top of the page, independent of the currently selected project. `DashboardUsageSummary` added to `web/src/lib/types.ts`; `npx tsc --noEmit` clean.

## 6. Tests

- [x] 6.1 Unit tests (against in-memory fakes) for each migrated service, covering the same scenarios the existing 31-test suite already covers — 34 tests pass (30 migrated + `UsageSummaryIsZeroWhenNothingUploaded`).
- [x] 6.2 Integration tests (`DynamoDbIntegrationTests.cs`, `[Trait("Category", "Integration")]`, skip unless `DYNAMODB_LOCAL_URL` is set — matching `AzureDevOpsIntegrationTests`' established pattern) against real DynamoDB Local for: the `Users` get-or-create conditional-write race (10 concurrent callers, one winner), `ApiTokens` revoke-by-id two-step (GSI query + primary-key update), and `UsageCounters` atomic increment under 20 concurrent ingest calls (none lost). Actually run against a real `docker compose up` DynamoDB Local instance — all 3 pass.
- [x] 6.3 Usage-metering-specific tests (`UsageMeteringTests.cs`, real HTTP ingest path): reports across multiple projects in one org are summed into one counter; reports in a different org never affect this org's counter; a report incremented against a prior calendar month doesn't affect the current period's counter; flag-proof reports increment their own counter separately from case reports. Zero-usage case covered by `DashboardServiceTests.UsageSummaryIsZeroWhenNothingUploaded`.

## 7. Cleanup

- [x] 7.1 Remove `HostedDbContext`, EF Core entity classes' persistence attributes/role, and the `Microsoft.EntityFrameworkCore.Sqlite`/`Npgsql.EntityFrameworkCore.PostgreSQL` package references once nothing depends on them. Confirmed zero remaining `EntityFrameworkCore`/`Npgsql`/`HostedDbContext` references anywhere under `hosted/`.
- [x] 7.2 Update `docs/installation-model.md` and `README.md`'s references to Postgres/SQLite as the hosted platform's data store — now DynamoDB, with the Terraform/DynamoDB Local setup commands.

## 8. Verification

- [x] 8.1 Run the full hosted API test suite and confirm all pass against the new repositories — 37/37 pass (34 unit + 3 real DynamoDB Local integration tests).
- [x] 8.2 Ran the full stack for real: `docker compose up -d` (DynamoDB Local) + hosted API pointed at it + `web/` dev server + the extended Cypress e2e spec — real Clerk sign-in, real project creation, real token issuance, and the "Usage this month" card rendering correctly (honest zero for a fresh org) all verified against the actual DynamoDB-backed API, not just the in-memory fake. Report-upload → counter-increment → dashboard-read path already proven end-to-end at the HTTP level by `UsageMeteringTests.cs` and against real DynamoDB by `DynamoDbIntegrationTests.cs`.
