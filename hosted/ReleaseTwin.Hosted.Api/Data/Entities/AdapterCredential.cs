namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// hosted-adapter-credentials: a project's stored execution credentials for one adapter (e.g. Azure
/// DevOps, LaunchDarkly). Rotation replaces this in place — there is no version history, unlike
/// JourneyVersion; the previous field values are not retrievable once replaced.
/// </summary>
public sealed class AdapterCredential
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Adapter { get; set; }

    /// <summary>Data-Protected ciphertext of the JSON-serialized field-name/value dictionary — never the plaintext.</summary>
    public required string EncryptedFields { get; set; }

    public required string LastSetByUserId { get; set; }
    public required string LastSetByDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
