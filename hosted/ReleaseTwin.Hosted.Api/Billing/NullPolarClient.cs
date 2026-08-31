namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>billing: a no-op <see cref="IPolarClient"/> for code paths that never reach a paid org (unit tests that construct services directly). Never registered in DI.</summary>
public sealed class NullPolarClient : IPolarClient
{
    public static readonly NullPolarClient Instance = new();

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Guid organizationId, Data.Entities.PlanTier tier, Data.Entities.BillingCadence cadence, CancellationToken cancellationToken = default) =>
        throw new PolarException("Polar is not configured in this context.");

    public Task<PortalSession> CreatePortalSessionAsync(string customerId, CancellationToken cancellationToken = default) =>
        throw new PolarException("Polar is not configured in this context.");

    public Task SetSubscriptionQuantityAsync(string subscriptionId, int quantity, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<SubscriptionInfo> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SubscriptionInfo(1, "active"));
}
