using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.LaunchDarkly;

public sealed class LaunchDarklyFeatureStateController : IFeatureStateController
{
    private readonly LaunchDarklyClient _client;

    internal LaunchDarklyFeatureStateController(LaunchDarklyClient client) => _client = client;

    public Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken) =>
        _client.SetFlagStateAsync(featureKey, enabled, cancellationToken);
}

internal sealed class ReadFeatureFlagOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly LaunchDarklyClient _client;
    private readonly string _flagKey;
    private readonly EvidenceBuffer _evidence = new();

    public ReadFeatureFlagOperation(LaunchDarklyClient client, string flagKey)
    {
        _client = client;
        _flagKey = flagKey;
    }

    public EvidenceContribution? DrainEvidence() => _evidence.Drain();

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        _evidence.Clear();
        var value = await _client.GetFlagStateAsync(_flagKey, cancellationToken);
        _evidence.Set(new EvidenceContribution(
            new AssertionDetail($"flag[{_flagKey}]", "true", value?.ToString()?.ToLowerInvariant() ?? "<not-found>")));
        return value == true
            ? OperationResult.Pass("true")
            : OperationResult.Fail(value is null ? "flag not found" : "false");
    }
}
