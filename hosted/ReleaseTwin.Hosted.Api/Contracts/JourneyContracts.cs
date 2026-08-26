namespace ReleaseTwin.Hosted.Api.Contracts;

/// <summary>
/// hosted-journeys: the CLI's own stable wire contract for fetching a pinned journey version,
/// deliberately decoupled from the hosted API's internal `JourneyVersion` entity so that entity is
/// free to evolve without breaking whatever CLI version a customer's CI happens to be running.
/// </summary>
public sealed class JourneyVersionResponse
{
    public required Guid JourneyId { get; init; }
    public required int Version { get; init; }
    public required string YamlContent { get; init; }
}
