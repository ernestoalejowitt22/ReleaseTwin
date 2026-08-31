namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>plan-tier-gating: one organization has exactly one tier at a time — no history, no concurrent plans — so this is a field, not a separate entity. Names match the <c>id</c> values in <c>hosted/plans.json</c> (case-insensitively). The earlier two-tier model's "Paid" maps to <see cref="Team"/> on read (see <see cref="Data.Repositories.OrganizationRepository"/>).</summary>
public enum PlanTier
{
    Free,
    Team,
    Enterprise,
}

/// <summary>
/// billing: a second axis alongside <see cref="PlanTier"/>. Entitlements are <c>tier ∧ status</c> —
/// <see cref="PastDue"/> keeps full tier entitlements for a grace window measured from
/// <see cref="Organization.BillingStatusSince"/>, then degrades to Free; <see cref="Canceled"/>
/// degrades immediately. Legacy rows and operator-set / hand-invoiced orgs read as <see cref="Active"/>.
/// </summary>
public enum BillingStatus
{
    Active,
    PastDue,
    Canceled,
}

/// <summary>billing: which Merchant-of-Record price the org's subscription was purchased on. Maps 1:1 to a <c>plans.json</c> price <c>interval</c>. Null for orgs with no paid subscription.</summary>
public enum BillingCadence
{
    Monthly,
    Annual,
}

public sealed class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public PlanTier PlanTier { get; set; } = PlanTier.Free;

    /// <summary>billing: defaults to <see cref="BillingStatus.Active"/> for new, legacy, and operator-set orgs.</summary>
    public BillingStatus BillingStatus { get; set; } = BillingStatus.Active;

    /// <summary>billing: when <see cref="BillingStatus"/> last changed, sourced from the Merchant-of-Record event timestamp. Defaults to <see cref="CreatedAt"/> for legacy rows.</summary>
    public DateTimeOffset BillingStatusSince { get; set; }

    /// <summary>billing: null when the org has no paid subscription.</summary>
    public BillingCadence? BillingCadence { get; set; }

    /// <summary>billing: Merchant-of-Record customer identifier; null until first checkout.</summary>
    public string? PolarCustomerId { get; set; }

    /// <summary>billing: Merchant-of-Record subscription identifier; null until first checkout. Its presence is what gates all quantity-sync and reconciliation behaviour.</summary>
    public string? PolarSubscriptionId { get; set; }

    /// <summary>onboarding-activation: false until the organization ingests its first real run. While
    /// false the dashboard shows a seeded sample project and a guided first-run panel; both disappear
    /// once it flips true, and it never flips back. Legacy rows read as false and are corrected on the
    /// org's next ingest.</summary>
    public bool HasIngestedRealRun { get; set; }
}
