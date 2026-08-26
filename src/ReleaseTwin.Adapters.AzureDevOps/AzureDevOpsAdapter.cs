using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>
/// The real (non-toy) second adapter for phase2-real-adapter: exercises Azure DevOps's actual
/// Work Items and variable-group REST APIs through the adapter-sdk contracts only.
///
/// Credentials are never hardcoded here (adapter-sdk's external-credentials requirement): the
/// caller resolves the PAT (environment variable, secret store, etc.) and passes it in via
/// <see cref="AzureDevOpsOptions"/>.
/// </summary>
public sealed class AzureDevOpsAdapter : IAdapterModule, IFeatureStateControllerSource, IDisposable
{
    private readonly AzureDevOpsClient _client;
    private readonly string _areaPath;
    private readonly int _variableGroupId;
    private readonly string _variableName;

    public AzureDevOpsAdapter(
        AzureDevOpsOptions options,
        string areaPath,
        int variableGroupId,
        string variableName = "release-proof-feature",
        HttpMessageHandler? handler = null)
    {
        _client = new AzureDevOpsClient(options, handler);
        _areaPath = areaPath;
        _variableGroupId = variableGroupId;
        _variableName = variableName;
    }

    public string Name => "azure-devops";

    /// <summary>Exposed so a composition can wire <see cref="ReleaseTwin.Core.FlagProofRunner"/> against this adapter's variable group.</summary>
    public IFeatureStateController FeatureStateController => new VariableGroupFeatureStateController(_client, _variableGroupId);

    /// <summary>
    /// Maps every operation/prerequisite/cleanup name <see cref="Register"/> would contribute to the
    /// capability it requires — accessible without constructing an adapter instance, so a caller can
    /// tell a case needs Azure DevOps even when this adapter isn't installed (adapter-sdk delta:
    /// graceful-capability-gating).
    /// </summary>
    public static IReadOnlyDictionary<string, string> KnownOperationCapabilities { get; } = new Dictionary<string, string>
    {
        ["azdo.areaPathExists"] = "http:azure-devops",
        ["azdo.createWorkItem"] = "http:azure-devops",
        ["azdo.getWorkItem"] = "http:azure-devops",
        ["azdo.transitionWorkItemState"] = "http:azure-devops",
        ["azdo.readFeatureVariable"] = "http:azure-devops",
        ["azdo.deleteWorkItem"] = "http:azure-devops",
    };

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddPrerequisite("azdo.areaPathExists", new AreaPathExistsCheck(_client, _areaPath))
            .AddOperation("azdo.createWorkItem", new CreateWorkItemOperation(_client))
            .AddOperation("azdo.getWorkItem", new GetWorkItemOperation(_client))
            .AddOperation("azdo.transitionWorkItemState", new TransitionWorkItemStateOperation(_client, targetState: "Done"))
            .AddOperation("azdo.readFeatureVariable", new ReadVariableGroupValueOperation(_client, _variableGroupId, _variableName))
            .AddCleanup("azdo.deleteWorkItem", new DeleteWorkItemCleanup(_client))
            .AddCapability("http:azure-devops")
            .AddCapability("flag-control:runtime");
    }

    public void Dispose() => _client.Dispose();
}
