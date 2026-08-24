using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>
/// design.md D3: stands in for a real feature flag by writing "true"/"false" to an Azure DevOps
/// variable group's variable. Explicitly plumbing-only — see D3's stated limitation.
/// </summary>
public sealed class VariableGroupFeatureStateController : IFeatureStateController
{
    private readonly AzureDevOpsClient _client;
    private readonly int _variableGroupId;

    internal VariableGroupFeatureStateController(AzureDevOpsClient client, int variableGroupId)
    {
        _client = client;
        _variableGroupId = variableGroupId;
    }

    public Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken) =>
        _client.SetVariableGroupValueAsync(_variableGroupId, featureKey, enabled ? "true" : "false", cancellationToken);
}

internal sealed class ReadVariableGroupValueOperation : IOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly int _variableGroupId;
    private readonly string _variableName;

    public ReadVariableGroupValueOperation(AzureDevOpsClient client, int variableGroupId, string variableName)
    {
        _client = client;
        _variableGroupId = variableGroupId;
        _variableName = variableName;
    }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var value = await _client.GetVariableGroupValueAsync(_variableGroupId, _variableName, cancellationToken);
        return value == "true"
            ? OperationResult.Pass(value)
            : OperationResult.Fail(value ?? "variable not set");
    }
}
