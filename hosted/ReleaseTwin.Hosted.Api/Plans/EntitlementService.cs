using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Plans;

/// <summary>
/// plan-catalog-and-entitlements: resolves an organization (or a bare <see cref="PlanTier"/>) to its
/// entitlement set from the <see cref="PlanCatalog"/>. Every feature gate makes its allow/deny
/// decision here — nothing compares a <see cref="PlanTier"/> value directly.
/// </summary>
public interface IEntitlementService
{
    Entitlements For(PlanTier tier);

    /// <summary>
    /// billing (D4): entitlements are <c>tier ∧ billing status</c>. After resolving the tier's catalog
    /// entitlements, a <see cref="BillingStatus.PastDue"/> org keeps them for a 14-day grace window
    /// measured from <see cref="Organization.BillingStatusSince"/>; past that, or when
    /// <see cref="BillingStatus.Canceled"/>, it drops to Free entitlements.
    /// </summary>
    Entitlements For(Organization? organization) => For(organization?.PlanTier ?? PlanTier.Free);

    /// <summary>The full catalog, for <c>GET /plans</c> and the dashboard's entitlement DTO.</summary>
    PlanCatalog Catalog { get; }
}

public sealed class EntitlementService : IEntitlementService
{
    private readonly ILogger<EntitlementService> _logger;

    public EntitlementService(PlanCatalog catalog, ILogger<EntitlementService> logger)
    {
        Catalog = catalog;
        _logger = logger;
    }

    public PlanCatalog Catalog { get; }

    /// <summary>billing (D4): grace window for a <see cref="BillingStatus.PastDue"/> org before its entitlements degrade to Free.</summary>
    public static readonly TimeSpan PastDueGraceWindow = TimeSpan.FromDays(14);

    public Entitlements For(Organization? organization)
    {
        if (organization is null)
        {
            return For(PlanTier.Free);
        }

        var tierEntitlements = For(organization.PlanTier);

        switch (organization.BillingStatus)
        {
            case BillingStatus.Active:
                return tierEntitlements;

            case BillingStatus.PastDue when DateTimeOffset.UtcNow <= organization.BillingStatusSince + PastDueGraceWindow:
                return tierEntitlements;

            case BillingStatus.PastDue:
            case BillingStatus.Canceled:
            default:
                // Grace window elapsed, or a hard cancellation: fall back to Free entitlements. The
                // stored tier is left untouched so a recovery event restores full access with no
                // re-provisioning.
                return For(PlanTier.Free);
        }
    }

    public Entitlements For(PlanTier tier)
    {
        var definition = Catalog.Find(tier);
        if (definition is not null)
        {
            return definition.Entitlements;
        }

        // A stored tier value that no longer maps to a catalog id (a removed tier, a corrupt row):
        // degrade to the least-privileged tier rather than throw.
        _logger.LogWarning("Plan tier {Tier} has no catalog entry; resolving to Free entitlements.", tier);
        return (Catalog.Find(PlanTier.Free)
            ?? throw new InvalidOperationException("Plan catalog has no 'free' tier to fall back to.")).Entitlements;
    }
}
