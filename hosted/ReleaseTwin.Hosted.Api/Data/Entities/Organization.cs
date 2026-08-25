namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>plan-tier-gating: one organization has exactly one tier at a time — no history, no concurrent plans — so this is a field, not a separate entity.</summary>
public enum PlanTier
{
    Free,
    Paid,
}

public sealed class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
}
