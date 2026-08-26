using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

internal sealed class CreateWorkItemOperation : IOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly string _workItemType;

    public CreateWorkItemOperation(AzureDevOpsClient client, string workItemType = "Task")
    {
        _client = client;
        _workItemType = workItemType;
    }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        var title = $"release-proof {context.Case.CaseId}";
        var patch = new[] { JsonPatchOperation.Add("/fields/System.Title", title) };

        try
        {
            var id = await _client.CreateWorkItemAsync(_workItemType, patch, cancellationToken);
            context.AdapterState["azdo.workItemId"] = id;
            return OperationResult.Pass($"created work item {id}");
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}

internal sealed class GetWorkItemOperation : IOperation
{
    private readonly AzureDevOpsClient _client;
    public GetWorkItemOperation(AzureDevOpsClient client) => _client = client;

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("azdo.workItemId", out var idObj) && idObj is int id)
        {
            var workItem = await _client.TryGetWorkItemAsync(id, cancellationToken);
            return workItem is null
                ? OperationResult.Fail($"work item {id} not found")
                : OperationResult.Pass(workItem.ToJsonString());
        }

        return OperationResult.Fail("no work item created in this case");
    }
}

internal sealed class TransitionWorkItemStateOperation : IOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly string _targetState;

    public TransitionWorkItemStateOperation(AzureDevOpsClient client, string targetState)
    {
        _client = client;
        _targetState = targetState;
    }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("azdo.workItemId", out var idObj) && idObj is int id)
        {
            try
            {
                await _client.UpdateWorkItemStateAsync(id, _targetState, cancellationToken);
                return OperationResult.Pass($"transitioned {id} to {_targetState}");
            }
            catch (HttpRequestException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        return OperationResult.Fail("no work item created in this case");
    }
}
