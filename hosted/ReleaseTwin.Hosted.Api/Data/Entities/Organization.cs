namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>plan-tier-gating: one organization has exactly one tier at a time — no history, no concurrent plans — so this is a field, not a separate entity. Names match the <c>id</c> values in <c>hosted/plans.json</c> (case-insensitively). The earlier two-tier model's "Paid" maps to <see cref="Team"/> on read (see <see cref="Data.Repositories.OrganizationRepository"/>).</summary>
public enum PlanTier
{
    Free,
    Team,
    Enterprise,
}

public sealed class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
}
