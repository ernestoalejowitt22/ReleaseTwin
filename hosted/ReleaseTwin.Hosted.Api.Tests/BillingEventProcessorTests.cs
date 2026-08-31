using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class BillingEventProcessorTests
{
    private sealed record Harness(
        BillingEventProcessor Processor,
        IOrganizationRepository Organizations,
        FakePolarClient Polar,
        Guid OrgId);

    private static async Task<Harness> NewAsync(PlanTier tier = PlanTier.Free)
    {
        var table = new InMemoryHostedTable();
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var users = new UserRepository(table);
        var tokens = new ApiTokenRepository(table);
        var polar = new FakePolarClient();
        var provisioning = new ProvisioningService(users, organizations, projects, tokens, new TokenService(), TestEntitlements.Service, polar);
        var processed = new ProcessedBillingEventRepository(table);
        var processor = new BillingEventProcessor(provisioning, organizations, processed, NullLogger<BillingEventProcessor>.Instance);

        var orgId = Guid.NewGuid();
        await table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = orgId,
            Name = "Acme",
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            PlanTier = tier,
        }));

        return new Harness(processor, organizations, polar, orgId);
    }

    private static string Event(string type, Guid orgId, string status = "active", string interval = "month", string id = "sub_1")
    {
        var data = "{\"id\":\"" + id + "\",\"customer_id\":\"cus_1\",\"status\":\"" + status
            + "\",\"recurring_interval\":\"" + interval
            + "\",\"modified_at\":\"2026-08-31T12:00:00Z\",\"metadata\":{\"organization_id\":\"" + orgId + "\"}}";
        return "{\"type\":\"" + type + "\",\"data\":" + data + "}";
    }

    [Fact]
    public async Task ActivationSetsTeamActiveAndStoresLinkage()
    {
        var h = await NewAsync();
        var outcome = await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId, interval: "year"));

        Assert.Equal(BillingEventOutcome.Processed, outcome);
        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(PlanTier.Team, org!.PlanTier);
        Assert.Equal(BillingStatus.Active, org.BillingStatus);
        Assert.Equal(BillingCadence.Annual, org.BillingCadence);
        Assert.Equal("cus_1", org.PolarCustomerId);
        Assert.Equal("sub_1", org.PolarSubscriptionId);
    }

    [Fact]
    public async Task PastDueSetsStatusWithEventTimestampAsSince()
    {
        var h = await NewAsync(PlanTier.Team);
        await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId));
        var outcome = await h.Processor.ProcessAsync("evt_2", Event("subscription.updated", h.OrgId, status: "past_due"));

        Assert.Equal(BillingEventOutcome.Processed, outcome);
        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(BillingStatus.PastDue, org!.BillingStatus);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero), org.BillingStatusSince);
        Assert.Equal(PlanTier.Team, org.PlanTier); // tier untouched
    }

    [Fact]
    public async Task CancellationSetsCanceled()
    {
        var h = await NewAsync(PlanTier.Team);
        await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId));
        await h.Processor.ProcessAsync("evt_2", Event("subscription.canceled", h.OrgId, status: "canceled"));

        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(BillingStatus.Canceled, org!.BillingStatus);
    }

    [Fact]
    public async Task RecoveryBackToActive()
    {
        var h = await NewAsync(PlanTier.Team);
        await h.Processor.ProcessAsync("evt_1", Event("subscription.updated", h.OrgId, status: "past_due"));
        await h.Processor.ProcessAsync("evt_2", Event("subscription.updated", h.OrgId, status: "active"));

        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(BillingStatus.Active, org!.BillingStatus);
    }

    [Fact]
    public async Task DuplicateDeliveryIsANoOp()
    {
        var h = await NewAsync();
        await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId));
        await h.Organizations.SetPlanTierAsync(h.OrgId, PlanTier.Free); // meddle

        var outcome = await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId));

        Assert.Equal(BillingEventOutcome.Duplicate, outcome);
        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(PlanTier.Free, org!.PlanTier); // not re-applied
    }

    [Fact]
    public async Task UnknownEventTypeIsRecordedButNoOp()
    {
        var h = await NewAsync();
        var outcome = await h.Processor.ProcessAsync("evt_1", Event("subscription.trialing", h.OrgId, status: "trialing"));

        Assert.Equal(BillingEventOutcome.Processed, outcome);
        var org = await h.Organizations.GetAsync(h.OrgId);
        Assert.Equal(PlanTier.Free, org!.PlanTier);
        Assert.Equal(BillingStatus.Active, org.BillingStatus);
    }

    [Fact]
    public async Task UnknownOrganizationIsNotRecordedAndCanRedeliver()
    {
        var h = await NewAsync();
        var strayOrg = Guid.NewGuid();
        var first = await h.Processor.ProcessAsync("evt_1", Event("subscription.active", strayOrg));
        Assert.Equal(BillingEventOutcome.OrganizationNotFound, first);

        // The event was not recorded — a redelivery after the org exists processes normally.
        var second = await h.Processor.ProcessAsync("evt_1", Event("subscription.active", h.OrgId));
        Assert.Equal(BillingEventOutcome.Processed, second);
    }

    [Fact]
    public void SignatureVerificationRejectsBadSignatures()
    {
        const string secret = "test-secret";
        const string id = "msg_1";
        const string ts = "1700000000";
        const string body = "{\"hello\":\"world\"}";

        // A hand-computed good signature round-trips.
        var key = System.Text.Encoding.UTF8.GetBytes(secret);
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        var good = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{id}.{ts}.{body}")));

        Assert.True(BillingWebhookSignature.Verify(secret, id, ts, $"v1,{good}", body));
        Assert.False(BillingWebhookSignature.Verify(secret, id, ts, "v1,not-the-signature", body));
        Assert.False(BillingWebhookSignature.Verify(secret, id, ts, null, body));
        Assert.False(BillingWebhookSignature.Verify(secret, id, ts, $"v1,{good}", body + "tampered"));
    }
}
