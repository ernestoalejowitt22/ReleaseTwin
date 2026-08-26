namespace ReleaseTwin.Hosted.Api.Contracts;

/// <summary>hosted-adapter-credentials: the CLI's own stable wire contract for a fetched adapter credential — deliberately decoupled from the hosted API's internal AdapterCredential entity, same rationale as every other cross-boundary contract in this codebase.</summary>
public sealed class AdapterCredentialResponse
{
    public required string Adapter { get; init; }
    public required IReadOnlyDictionary<string, string> Fields { get; init; }
}
