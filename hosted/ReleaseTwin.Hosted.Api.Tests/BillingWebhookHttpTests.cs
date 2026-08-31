using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class BillingWebhookHttpTests
{
    private static string Sign(string secret, string id, string ts, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "v1," + Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{id}.{ts}.{body}")));
    }

    private static HttpRequestMessage Webhook(string id, string sig, string body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("webhook-id", id);
        req.Headers.Add("webhook-timestamp", "1700000000");
        req.Headers.Add("webhook-signature", sig);
        return req;
    }

    [Fact]
    public async Task BillingNotConfiguredReturns503()
    {
        using var factory = new CustomWebApplicationFactory { ConfigureBilling = false };
        var client = factory.CreateClient();

        var response = await client.SendAsync(Webhook("evt_1", "v1,whatever", "{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task BadSignatureIsRejected()
    {
        using var factory = new CustomWebApplicationFactory { ConfigureBilling = true };
        var client = factory.CreateClient();

        var response = await client.SendAsync(Webhook("evt_1", "v1,not-valid", "{\"type\":\"subscription.active\"}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidActivationMovesOrgToTeamAndDuplicateIsNoOp()
    {
        using var factory = new CustomWebApplicationFactory { ConfigureBilling = true };
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var orgs = new OrganizationRepository(table);

        var orgId = Guid.NewGuid();
        await table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = orgId, Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = PlanTier.Free,
        }));

        var client = factory.CreateClient();
        var body = "{\"type\":\"subscription.active\",\"data\":{\"id\":\"sub_1\",\"customer_id\":\"cus_1\","
            + "\"status\":\"active\",\"recurring_interval\":\"month\",\"metadata\":{\"organization_id\":\""
            + orgId + "\"}}}";
        var sig = Sign("test-webhook-secret", "evt_1", "1700000000", body);

        var first = await client.SendAsync(Webhook("evt_1", sig, body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var org = await orgs.GetAsync(orgId);
        Assert.Equal(PlanTier.Team, org!.PlanTier);
        Assert.Equal(BillingStatus.Active, org.BillingStatus);

        // Duplicate delivery — still 200, still Team, nothing re-applied.
        await orgs.SetPlanTierAsync(orgId, PlanTier.Free);
        var second = await client.SendAsync(Webhook("evt_1", sig, body));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(PlanTier.Free, (await orgs.GetAsync(orgId))!.PlanTier);
    }
}
