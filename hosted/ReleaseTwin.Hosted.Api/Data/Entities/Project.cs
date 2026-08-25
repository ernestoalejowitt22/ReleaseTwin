namespace ReleaseTwin.Hosted.Api.Data.Entities;

public sealed class Project
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }
}
