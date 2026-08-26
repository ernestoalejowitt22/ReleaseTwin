using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.LaunchDarkly;

public sealed class LaunchDarklyFeatureStateController : IFeatureStateController
{
    private readonly LaunchDarklyClient _client;

    internal LaunchDarklyFeatureStateController(LaunchDarklyClient client) => _client = client;

    public Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken) =>
        _client.SetFlagStateAsync(featureKey, enabled, cancellationToken);
}

internal sealed class ReadFeatureFlagOperation : IOperation
{
    private readonly LaunchDarklyClient _client;
    private readonly string _flagKey;

    public ReadFeatureFlagOperation(LaunchDarklyClient client, string flagKey)
    {
        _client = client;
        _flagKey = flagKey;
    }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        var value = await _client.GetFlagStateAsync(_flagKey, cancellationToken);
        return value == true
            ? OperationResult.Pass("true")
            : OperationResult.Fail(value is null ? "flag not found" : "false");
    }
}
