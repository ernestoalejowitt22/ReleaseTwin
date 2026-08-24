namespace ReleaseTwin.Hosted.Api.Data.Entities;

public sealed class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Project> Projects { get; set; } = new();
    public List<AppUser> Users { get; set; } = new();
}
