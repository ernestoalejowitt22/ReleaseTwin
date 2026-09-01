using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// security-hardening-pre-pilot D7 (abuse-rate-limiting): the ingest / share-link / billing-webhook
/// ceilings. Limits are driven tiny via host settings so a burst is a handful of requests, not
/// thousands.
/// </summary>
public class RateLimitingTests
{
    private static WebApplicationFactory<Program> Factory(params (string Key, string Value)[] settings)
    {
        var f = new CustomWebApplicationFactory();
        return f.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });
    }

    private static async Task<(HttpClient Client, Guid ProjectId, Guid OrgId)> IngestClientAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "t", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        return (client, project.Id, user.OrganizationId);
    }

    private static object Report() => new
    {
        caseId = "C1", oracleLocator = "t/1", fixtureSha256 = "abc", passed = true,
        cleanupStatus = "AllSucceeded", durationMs = 1,
    };

    [Fact]
    public async Task IngestBurstPastCeilingGets429AndStoresNothingForRejected()
    {
        using var factory = Factory(("RateLimiting:Ingest:TokenLimit", "2"));
        var (client, projectId, orgId) = await IngestClientAsync(factory);

        var first = await client.PostAsJsonAsync("/api/ingest/case-report", Report());
        var second = await client.PostAsJsonAsync("/api/ingest/case-report", Report());
        var third = await client.PostAsJsonAsync("/api/ingest/case-report", Report());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.NotNull(third.Headers.RetryAfter);

        using var scope = factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageCounterRepository>();
        Assert.Equal(2, (await reports.ListByProjectAsync(projectId)).Count); // the rejected one is not stored
        Assert.Equal(2, (await usage.GetAsync(orgId, Keys.CurrentUtcPeriod())).CaseReportCount);
    }

    [Fact]
    public async Task OneTokenBeingThrottledDoesNotAffectAnother()
    {
        using var factory = Factory(("RateLimiting:Ingest:TokenLimit", "1"));
        var (clientA, _, _) = await IngestClientAsync(factory);
        var (clientB, _, _) = await IngestClientAsync(factory);

        Assert.Equal(HttpStatusCode.Created, (await clientA.PostAsJsonAsync("/api/ingest/case-report", Report())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await clientA.PostAsJsonAsync("/api/ingest/case-report", Report())).StatusCode);

        // Token B has its own bucket.
        Assert.Equal(HttpStatusCode.Created, (await clientB.PostAsJsonAsync("/api/ingest/case-report", Report())).StatusCode);
    }

    [Fact]
    public async Task NormalSizedSuiteIsNeverThrottledAtTheDefaultCeiling()
    {
        using var factory = Factory(); // default ceilings
        var (client, _, _) = await IngestClientAsync(factory);

        for (var i = 0; i < 50; i++)
        {
            var response = await client.PostAsJsonAsync("/api/ingest/case-report", Report());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task DisabledSwitchTurnsOffAllLimiting()
    {
        using var factory = Factory(
            ("RateLimiting:Enabled", "false"),
            ("RateLimiting:Ingest:TokenLimit", "1"));
        var (client, _, _) = await IngestClientAsync(factory);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/ingest/case-report", Report())).StatusCode);
        }
    }

    [Fact]
    public async Task ShareLinkFloodFromOneAddressIsThrottledButAnotherAddressIsFine()
    {
        using var factory = Factory(("RateLimiting:ShareLinks:PermitLimit", "2"));
        var client = factory.CreateClient();

        HttpRequestMessage Req(string ip)
        {
            var r = new HttpRequestMessage(HttpMethod.Get, "/api/shared-runs/some-token");
            r.Headers.Add("X-Forwarded-For", ip);
            return r;
        }

        // Unknown/invalid token → 404, but the limiter still counts the request.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Req("203.0.113.7"))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Req("203.0.113.7"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Req("203.0.113.7"))).StatusCode);

        // A different address is unaffected.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Req("198.51.100.9"))).StatusCode);
    }

    [Fact]
    public async Task BillingWebhookFloodIsRejectedBeforeSignatureVerification()
    {
        using var factory = Factory(("RateLimiting:BillingWebhook:PermitLimit", "1"));
        var client = factory.CreateClient();

        HttpRequestMessage Req()
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
            {
                Content = new StringContent("{}"),
            };
            r.Headers.Add("X-Forwarded-For", "203.0.113.50");
            r.Headers.Add("webhook-id", "e");
            r.Headers.Add("webhook-timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            r.Headers.Add("webhook-signature", "v1,bogus");
            return r;
        }

        // Billing not configured here → the first would be 503; the point is the *second* is 429,
        // shed before the handler runs.
        await client.SendAsync(Req());
        var flooded = await client.SendAsync(Req());
        Assert.Equal(HttpStatusCode.TooManyRequests, flooded.StatusCode);
    }
}
