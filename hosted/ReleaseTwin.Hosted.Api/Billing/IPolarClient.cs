using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>A Merchant-of-Record hosted-checkout session the customer is redirected to.</summary>
public sealed record CheckoutSession(string Url);

/// <summary>A Merchant-of-Record customer-portal session the customer is redirected to.</summary>
public sealed record PortalSession(string Url);

/// <summary>The subset of a Merchant-of-Record subscription the reconciliation job needs.</summary>
public sealed record SubscriptionInfo(int Quantity, string Status);

/// <summary>
/// billing: the entire seam between the hosted platform and the Merchant of Record (Polar). No Polar
/// SDK type, JSON shape, or header leaks past this interface — a future provider swap (design.md: the
/// Lemon Squeezy fallback) is contained to <c>Billing/</c>. All methods throw <see cref="PolarException"/>
/// on a non-success response so callers can fail closed with a portal-pointing message.
/// </summary>
public interface IPolarClient
{
    /// <summary>Creates a hosted checkout for <paramref name="tier"/> at <paramref name="cadence"/>. The org id rides through as checkout metadata so the webhook can resolve it back.</summary>
    Task<CheckoutSession> CreateCheckoutSessionAsync(Guid organizationId, PlanTier tier, BillingCadence cadence, CancellationToken cancellationToken = default);

    Task<PortalSession> CreatePortalSessionAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>Sets the per-project subscription quantity. Used synchronously on project create/delete and by the nightly reconciliation job.</summary>
    Task SetSubscriptionQuantityAsync(string subscriptionId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Reads the current subscription quantity + status, for the nightly reconciliation job's drift check.</summary>
    Task<SubscriptionInfo> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

/// <summary>billing: any failure talking to the Merchant of Record — a declined proration charge, an API error, a transport failure. Never leaks a Polar-specific type.</summary>
public sealed class PolarException : Exception
{
    public PolarException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
