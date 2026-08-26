namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// hosted-journeys: one immutable version of a journey's content. Version numbers are assigned
/// sequentially starting at 1 and never reused; editing a journey always creates a new version
/// rather than mutating an existing one (spec: "A saved journey version is immutable").
/// CreatedByDisplayName is a denormalized snapshot at creation time, not a live join to AppUser —
/// version history should show who it was at the time, not whoever that account is named today.
/// </summary>
public sealed class JourneyVersion
{
    public Guid Id { get; set; }
    public Guid JourneyId { get; set; }
    public int Version { get; set; }
    public required string YamlContent { get; set; }
    public required string CreatedByUserId { get; set; }
    public required string CreatedByDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
