namespace ReleaseTwin.Hosted.Api.Contracts;

/// <summary>hosted-project-secrets: the CLI's own stable wire contract for a project's fetched secrets — deliberately decoupled from the hosted API's internal ProjectSecret entity, same rationale as every other cross-boundary contract in this codebase.</summary>
public sealed class ProjectSecretsResponse
{
    public required IReadOnlyDictionary<string, string> Secrets { get; init; }
}
