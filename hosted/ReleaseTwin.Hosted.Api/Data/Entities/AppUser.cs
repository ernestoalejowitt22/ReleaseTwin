namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>A signed-up human, authenticated via a managed auth provider (Clerk) — web-session auth domain.</summary>
public sealed class AppUser
{
    public Guid Id { get; set; }
    public required string ClerkUserId { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }
}
