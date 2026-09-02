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
    // security-hardening-pre-pilot D4: the webhook now enforces timestamp freshness, so tests sign
    // and send with a current timestamp.
    private static readonly string CurrentTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    private static string Sign(string secret, string id, string body, string? ts = null)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "v1," + Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{id}.{ts ?? CurrentTs}.{body}")));
    }

    private static HttpRequestMessage Webhook(string id, string sig, string body, string? ts = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("webhook-id", id);
        req.Headers.Add("webhook-timestamp", ts ?? CurrentTs);
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
        var sig = Sign("test-webhook-secret", "evt_1", body);

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

    // security-hardening-pre-pilot D4: a validly-signed but stale delivery is rejected and applies nothing.
    [Fact]
    public async Task StaleButValidlySignedWebhookIsRejectedAndNotProcessed()
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

        var oldTs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        var sig = Sign("test-webhook-secret", "evt_stale", body, oldTs);

        var response = await client.SendAsync(Webhook("evt_stale", sig, body, oldTs));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(PlanTier.Free, (await orgs.GetAsync(orgId))!.PlanTier);

        // Not recorded as processed → a fresh redelivery of the same event id still works.
        var freshSig = Sign("test-webhook-secret", "evt_stale", body);
        var redelivery = await client.SendAsync(Webhook("evt_stale", freshSig, body));
        Assert.Equal(HttpStatusCode.OK, redelivery.StatusCode);
        Assert.Equal(PlanTier.Team, (await orgs.GetAsync(orgId))!.PlanTier);
    }
}
