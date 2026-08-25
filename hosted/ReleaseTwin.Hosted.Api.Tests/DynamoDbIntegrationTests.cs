using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// usage-metering tasks.md 6.2: exercises real DynamoDB semantics the in-memory fake can only
/// approximate — conditional-write races, the revoke-by-id GSI two-step, and atomic counter
/// increments under real concurrency. Skips (no-ops, matching AzureDevOpsIntegrationTests'
/// established pattern) unless DYNAMODB_LOCAL_URL is set to a reachable DynamoDB Local endpoint —
/// e.g. `docker compose up -d` in hosted/, then `DYNAMODB_LOCAL_URL=http://localhost:8000`.
///
/// Filter to just these with: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class DynamoDbIntegrationTests
{
    private static bool TryGetLocalEndpoint(out string serviceUrl)
    {
        serviceUrl = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_URL") ?? "";
        return !string.IsNullOrWhiteSpace(serviceUrl);
    }

    private static async Task<IHostedTable> NewRealTableAsync(string serviceUrl)
    {
        var client = new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
        var tableName = "ReleaseTwinHostedIntegrationTest-" + Guid.NewGuid().ToString("N");
        await TableProvisioning.EnsureTableExistsAsync(client, tableName);
        return new DynamoDbHostedTable(client, tableName);
    }

    // Scenario: concurrent first-logins for the same Clerk user race for organization creation
    [Fact]
    public async Task ConcurrentGetOrCreateUserRaceIsResolvedWithoutDuplicateOrganizations()
    {
        if (!TryGetLocalEndpoint(out var serviceUrl))
        {
            // No DynamoDB Local endpoint configured — see class doc comment.
            return;
        }

        var table = await NewRealTableAsync(serviceUrl);
        var users = new UserRepository(table);
        const string clerkUserId = "clerk-race-1";

        async Task<AppUser> GetOrCreate()
        {
            var existing = await users.GetByClerkUserIdAsync(clerkUserId);
            if (existing is not null)
            {
                return existing;
            }

            var org = new Organization { Id = Guid.NewGuid(), Name = "Racer's org", CreatedAt = DateTimeOffset.UtcNow };
            var user = new AppUser { Id = Guid.NewGuid(), ClerkUserId = clerkUserId, DisplayName = "Racer", CreatedAt = DateTimeOffset.UtcNow, OrganizationId = org.Id };
            try
            {
                await users.CreateWithOrganizationAsync(org, user);
                return user;
            }
            catch (ConditionalCheckFailedException)
            {
                return await users.GetByClerkUserIdAsync(clerkUserId) ?? throw new InvalidOperationException("Race lost but no user found.");
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => GetOrCreate()));

        Assert.All(results, r => Assert.Equal(results[0].OrganizationId, r.OrganizationId));
    }

    // Scenario: Revoking by id (GSI2 lookup, then primary-key update)
    [Fact]
    public async Task RevokeByIdFindsTheTokenThroughGsi2AndUpdatesThePrimaryItem()
    {
        if (!TryGetLocalEndpoint(out var serviceUrl))
        {
            return;
        }

        var table = await NewRealTableAsync(serviceUrl);
        var tokens = new ApiTokenRepository(table);
        var token = await tokens.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "hash-" + Guid.NewGuid(), "rtw_abcd1234");

        await tokens.RevokeAsync(token.Id);

        var reloaded = await tokens.GetByHashAsync(token.TokenHash);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsRevoked);
    }

    // Scenario: Reports across multiple projects in the same organization are combined (real atomic ADD under concurrency)
    [Fact]
    public async Task ConcurrentIncrementsAreNotLostUnderRealAtomicAdd()
    {
        if (!TryGetLocalEndpoint(out var serviceUrl))
        {
            return;
        }

        var table = await NewRealTableAsync(serviceUrl);
        var usage = new UsageCounterRepository(table);
        var orgId = Guid.NewGuid();
        var period = Keys.CurrentUtcPeriod();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => usage.IncrementAsync(orgId, period, isFlagProof: false)));

        var counter = await usage.GetAsync(orgId, period);
        Assert.Equal(20, counter.CaseReportCount);
    }
}
