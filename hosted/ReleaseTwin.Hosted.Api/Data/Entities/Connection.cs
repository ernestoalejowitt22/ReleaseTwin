namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// project-connections spec: display metadata only, labeling a project with an external repo chosen
/// via a real OAuth-driven picker. No credential is ever a column here, by construction — the access
/// token used to fetch the repo list during connection is never persisted anywhere.
/// </summary>
public sealed class Connection
{
    public Guid Id { get; set; }

    /// <summary>Not an enum: adding a second provider later (Bitbucket, Azure DevOps) shouldn't require a migration touching this column's type.</summary>
    public required string Provider { get; set; }

    /// <summary>e.g. "acme-corp/checkout-service" — the external repo's identifier, nothing more.</summary>
    public required string ExternalRepo { get; set; }

    public DateTimeOffset ConnectedAt { get; set; }

    public Guid ProjectId { get; set; }
}
