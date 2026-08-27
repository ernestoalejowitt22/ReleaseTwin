namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// hosted-project-secrets: a project's stored value for one arbitrary, customer-chosen secret name —
/// generalizes AdapterCredential's fixed-manifest shape to any name a journey/case's `${VAR_NAME}`
/// might reference. Rotation replaces this in place — there is no version history, same as
/// AdapterCredential; the previous value is not retrievable once replaced.
/// </summary>
public sealed class ProjectSecret
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }

    /// <summary>Data-Protected ciphertext of the value — never the plaintext.</summary>
    public required string EncryptedValue { get; set; }

    public required string LastSetByUserId { get; set; }
    public required string LastSetByDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
