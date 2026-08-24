namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// account-provisioning spec: self-serve issued, project-scoped, self-serve revocable. Only a SHA-256
/// hash of the token is ever stored — the raw value is shown once at issuance and never persisted.
/// </summary>
public sealed class ApiToken
{
    public Guid Id { get; set; }
    public required string TokenHash { get; set; }

    /// <summary>Short, non-secret prefix of the raw token, stored so a user can recognize which token is which in a list.</summary>
    public required string DisplayPrefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public bool IsRevoked => RevokedAt is not null;
}
