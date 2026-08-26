namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// hosted-journeys: a named journey belonging to one project. The journey itself carries no
/// content — every version is immutable content, and the journey is just the identity/name that
/// versions accumulate under.
/// </summary>
public sealed class Journey
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
