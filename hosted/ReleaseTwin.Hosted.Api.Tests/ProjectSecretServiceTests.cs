using Microsoft.AspNetCore.DataProtection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-project-secrets: set/rotate/revoke, encryption round-trip, and the Paid-tier gate.</summary>
public class ProjectSecretServiceTests
{
    /// <summary>
    /// One shared in-memory table backs every repository built here, so an organization created
    /// through <see cref="ProvisioningService"/> is visible to the <see cref="ProjectSecretService"/>
    /// under test — same convention <c>PlanTierGatingTests</c> already follows.
    /// </summary>
    private sealed record Fixture(ProjectSecretService Service, IProjectSecretRepository Repository, ProvisioningService Provisioning, IOrganizationRepository Organizations);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var secretRepository = new ProjectSecretRepository(table);
        var organizations = new OrganizationRepository(table);
        var provisioning = new ProvisioningService(new UserRepository(table), organizations, new ProjectRepository(table), new ApiTokenRepository(table), new TokenService());
        var service = new ProjectSecretService(secretRepository, organizations, new EphemeralDataProtectionProvider());
        return new Fixture(service, secretRepository, provisioning, organizations);
    }

    private static async Task<Guid> NewPaidOrganizationAsync(Fixture fixture)
    {
        var user = await fixture.Provisioning.GetOrCreateUserAsync("clerk-" + Guid.NewGuid(), "Alice", null);
        await fixture.Organizations.SetPlanTierAsync(user.OrganizationId, PlanTier.Paid);
        return user.OrganizationId;
    }

    [Fact]
    public async Task SettingASecretForAPaidOrganizationSucceeds()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();

        var secret = await fixture.Service.SetAsync(orgId, projectId, "NAHA_E2E_SECRET", "real-value", "user-1", "Alice");

        Assert.Equal("NAHA_E2E_SECRET", secret.Name);
    }

    // Scenario: Storing a secret requires the Paid tier
    [Fact]
    public async Task SettingASecretForAFreeOrganizationIsRejected()
    {
        var fixture = NewFixture();
        // Free is the default tier a freshly-provisioned organization starts on — no upgrade call.
        var user = await fixture.Provisioning.GetOrCreateUserAsync("clerk-" + Guid.NewGuid(), "Bob", null);

        await Assert.ThrowsAsync<PaidTierRequiredException>(() =>
            fixture.Service.SetAsync(user.OrganizationId, Guid.NewGuid(), "SOME_SECRET", "value", "user-1", "Bob"));
    }

    // Scenario: Raw storage never contains a plaintext value
    [Fact]
    public async Task StoredRepresentationIsNeverThePlaintextValue()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();

        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "super-secret-value", "user-1", "Alice");

        var stored = await fixture.Repository.GetAsync(projectId, "SOME_SECRET");
        Assert.NotNull(stored);
        Assert.DoesNotContain("super-secret-value", stored!.EncryptedValue);
    }

    [Fact]
    public async Task FetchingAfterSettingReturnsTheOriginalPlaintextValue()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();

        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "super-secret-value", "user-1", "Alice");
        var all = await fixture.Service.GetAllDecryptedAsync(projectId);

        Assert.Equal("super-secret-value", all["SOME_SECRET"]);
    }

    // Scenario: Rotating replaces the value entirely
    [Fact]
    public async Task RotatingReplacesThePreviousValueEntirely()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();
        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "old-value", "user-1", "Alice");

        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "new-value", "user-2", "Bob");

        var all = await fixture.Service.GetAllDecryptedAsync(projectId);
        Assert.Equal("new-value", all["SOME_SECRET"]);
    }

    // Scenario: Revoking removes the secret from future fetches
    [Fact]
    public async Task RevokingRemovesTheSecretFromFutureFetches()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();
        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "value", "user-1", "Alice");

        await fixture.Service.DeleteAsync(projectId, "SOME_SECRET");

        var all = await fixture.Service.GetAllDecryptedAsync(projectId);
        Assert.False(all.ContainsKey("SOME_SECRET"));
    }

    // Scenario: A project with no stored secrets is a clear, distinct outcome
    [Fact]
    public async Task FetchingAProjectWithNothingStoredReturnsAnEmptySet()
    {
        var fixture = NewFixture();

        var all = await fixture.Service.GetAllDecryptedAsync(Guid.NewGuid());

        Assert.Empty(all);
    }

    [Fact]
    public async Task ListingMetadataNeverIncludesTheValue()
    {
        var fixture = NewFixture();
        var orgId = await NewPaidOrganizationAsync(fixture);
        var projectId = Guid.NewGuid();
        await fixture.Service.SetAsync(orgId, projectId, "SOME_SECRET", "super-secret-value", "user-1", "Alice");

        var list = await fixture.Service.ListMetadataAsync(projectId);

        var entry = Assert.Single(list);
        Assert.Equal("SOME_SECRET", entry.Name);
        Assert.Equal("Alice", entry.LastSetByDisplayName);
        Assert.DoesNotContain("super-secret-value", entry.EncryptedValue);
    }
}
