using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// billing: records every call and lets a test script failures. Stands in for the real HTTP
/// <see cref="IPolarClient"/> everywhere a test exercises the upgrade / webhook / quantity-sync paths.
/// </summary>
public sealed class FakePolarClient : IPolarClient
{
    public sealed record CheckoutCall(Guid OrganizationId, PlanTier Tier, BillingCadence Cadence);
    public sealed record QuantityCall(string SubscriptionId, int Quantity);

    public List<CheckoutCall> Checkouts { get; } = [];
    public List<string> PortalSessions { get; } = [];
    public List<QuantityCall> QuantityUpdates { get; } = [];

    /// <summary>When set, the next matching call throws it instead of succeeding.</summary>
    public Exception? FailNextQuantityUpdate { get; set; }
    public Exception? FailCheckout { get; set; }
    public Exception? FailPortal { get; set; }

    public string CheckoutUrl { get; set; } = "https://checkout.polar.test/session";
    public string PortalUrl { get; set; } = "https://portal.polar.test/session";

    /// <summary>What <see cref="GetSubscriptionAsync"/> reports, keyed by subscription id. Missing ⇒ quantity 1, active.</summary>
    public Dictionary<string, SubscriptionInfo> Subscriptions { get; } = [];

    public Task<SubscriptionInfo> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subscriptions.TryGetValue(subscriptionId, out var info) ? info : new SubscriptionInfo(1, "active"));

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Guid organizationId, PlanTier tier, BillingCadence cadence, CancellationToken cancellationToken = default)
    {
        if (FailCheckout is not null)
        {
            return Task.FromException<CheckoutSession>(FailCheckout);
        }

        Checkouts.Add(new CheckoutCall(organizationId, tier, cadence));
        return Task.FromResult(new CheckoutSession(CheckoutUrl));
    }

    public Task<PortalSession> CreatePortalSessionAsync(string customerId, CancellationToken cancellationToken = default)
    {
        if (FailPortal is not null)
        {
            return Task.FromException<PortalSession>(FailPortal);
        }

        PortalSessions.Add(customerId);
        return Task.FromResult(new PortalSession(PortalUrl));
    }

    public Task SetSubscriptionQuantityAsync(string subscriptionId, int quantity, CancellationToken cancellationToken = default)
    {
        if (FailNextQuantityUpdate is not null)
        {
            var ex = FailNextQuantityUpdate;
            FailNextQuantityUpdate = null;
            return Task.FromException(ex);
        }

        QuantityUpdates.Add(new QuantityCall(subscriptionId, quantity));
        return Task.CompletedTask;
    }
}
