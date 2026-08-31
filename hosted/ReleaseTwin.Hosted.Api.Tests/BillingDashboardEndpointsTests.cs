using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class BillingDashboardEndpointsTests
{
    private static async Task<(OrganizationRepository Orgs, Guid OrgId)> SeedOrgAsync(CustomWebApplicationFactory factory, PlanTier tier = PlanTier.Free, string? polarCustomerId = null)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var orgs = new OrganizationRepository(table);
        var orgId = Guid.NewGuid();
        await table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = orgId, Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier,
        }));
        if (polarCustomerId is not null)
        {
            await orgs.SetBillingAsync(orgId, BillingStatus.Active, DateTimeOffset.UtcNow, BillingCadence.Monthly, polarCustomerId, "sub_1");
        }
        return (orgs, orgId);
    }

    [Fact]
    public async Task UpgradeReturnsCheckoutUrlAndDoesNotChangeTier()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true };
        var (orgs, orgId) = await SeedOrgAsync(factory);
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsJsonAsync("/api/dashboard/upgrade", new { cadence = "Annual" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal(factory.PolarClient.CheckoutUrl, body!.CheckoutUrl);

        // design.md D2: the webhook is the only writer of the tier — checkout must not touch it.
        Assert.Equal(PlanTier.Free, (await orgs.GetAsync(orgId))!.PlanTier);

        var call = Assert.Single(factory.PolarClient.Checkouts);
        Assert.Equal(orgId, call.OrganizationId);
        Assert.Equal(BillingCadence.Annual, call.Cadence);
        Assert.Equal(PlanTier.Team, call.Tier);
    }

    [Fact]
    public async Task UpgradeDefaultsToMonthlyCadence()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true };
        var (_, orgId) = await SeedOrgAsync(factory);
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsync("/api/dashboard/upgrade", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BillingCadence.Monthly, factory.PolarClient.Checkouts.Single().Cadence);
    }

    [Fact]
    public async Task UpgradeIs503WhenBillingNotConfigured()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = false };
        var (_, orgId) = await SeedOrgAsync(factory);
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsJsonAsync("/api/dashboard/upgrade", new { cadence = "Monthly" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(factory.PolarClient.Checkouts);
    }

    [Fact]
    public async Task UpgradeIs503WhenConfiguredButButtonNotYetEnabled()
    {
        // design.md staging: the webhook is live (ConfigureBilling) but the customer-facing button
        // stays closed until a sandbox checkout has been verified.
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true, UpgradeButtonEnabled = false };
        var (_, orgId) = await SeedOrgAsync(factory);
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsJsonAsync("/api/dashboard/upgrade", new { cadence = "Monthly" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(factory.PolarClient.Checkouts);
    }

    [Fact]
    public async Task UpgradeRequiresAuthentication()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dashboard/upgrade", new { cadence = "Monthly" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BillingPortalIs400WhenOrgHasNoCustomerId()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true };
        var (_, orgId) = await SeedOrgAsync(factory);
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsync("/api/dashboard/billing-portal", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.PolarClient.PortalSessions);
    }

    [Fact]
    public async Task BillingPortalReturnsPortalUrlForLinkedOrg()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true, ConfigureBilling = true };
        var (_, orgId) = await SeedOrgAsync(factory, PlanTier.Team, polarCustomerId: "cus_42");
        var client = factory.CreateClientForOrg(orgId);

        var response = await client.PostAsync("/api/dashboard/billing-portal", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PortalResponse>();
        Assert.Equal(factory.PolarClient.PortalUrl, body!.PortalUrl);
        Assert.Equal("cus_42", Assert.Single(factory.PolarClient.PortalSessions));
    }

    private sealed record CheckoutResponse(string CheckoutUrl);
    private sealed record PortalResponse(string PortalUrl);
}
