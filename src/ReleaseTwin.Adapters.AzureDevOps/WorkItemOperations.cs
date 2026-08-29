using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>evidence-capture: adapter-defined evidence for an Azure DevOps work-item step.</summary>
public sealed record AzureDevOpsStepEvidence(string Action, int? WorkItemId, string? Target, string Outcome);

internal sealed class CreateWorkItemOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly string _workItemType;
    private readonly EvidenceBuffer _evidence = new();

    public CreateWorkItemOperation(AzureDevOpsClient client, string workItemType = "Task")
    {
        _client = client;
        _workItemType = workItemType;
    }

    public EvidenceContribution? DrainEvidence() => _evidence.Drain();

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        _evidence.Clear();
        var title = $"release-proof {context.Case.CaseId}";
        var patch = new[] { JsonPatchOperation.Add("/fields/System.Title", title) };

        try
        {
            var id = await _client.CreateWorkItemAsync(_workItemType, patch, cancellationToken);
            context.AdapterState["azdo.workItemId"] = id;
            _evidence.SetAdapter(new AzureDevOpsStepEvidence("createWorkItem", id, _workItemType, "created"));
            return OperationResult.Pass($"created work item {id}");
        }
        catch (HttpRequestException ex)
        {
            _evidence.SetAdapter(new AzureDevOpsStepEvidence("createWorkItem", null, _workItemType, $"error: {ex.Message}"));
            return OperationResult.Fail(ex.Message);
        }
    }
}

internal sealed class GetWorkItemOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly EvidenceBuffer _evidence = new();
    public GetWorkItemOperation(AzureDevOpsClient client) => _client = client;

    public EvidenceContribution? DrainEvidence() => _evidence.Drain();

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        _evidence.Clear();
        if (context.AdapterState.TryGetValue("azdo.workItemId", out var idObj) && idObj is int id)
        {
            var workItem = await _client.TryGetWorkItemAsync(id, cancellationToken);
            _evidence.SetAdapter(new AzureDevOpsStepEvidence("getWorkItem", id, null, workItem is null ? "not-found" : "found"));
            return workItem is null
                ? OperationResult.Fail($"work item {id} not found")
                : OperationResult.Pass(workItem.ToJsonString());
        }

        _evidence.SetAdapter(new AzureDevOpsStepEvidence("getWorkItem", null, null, "no-work-item"));
        return OperationResult.Fail("no work item created in this case");
    }
}

internal sealed class TransitionWorkItemStateOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly AzureDevOpsClient _client;
    private readonly string _targetState;
    private readonly EvidenceBuffer _evidence = new();

    public TransitionWorkItemStateOperation(AzureDevOpsClient client, string targetState)
    {
        _client = client;
        _targetState = targetState;
    }

    public EvidenceContribution? DrainEvidence() => _evidence.Drain();

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        _evidence.Clear();
        if (context.AdapterState.TryGetValue("azdo.workItemId", out var idObj) && idObj is int id)
        {
            try
            {
                await _client.UpdateWorkItemStateAsync(id, _targetState, cancellationToken);
                _evidence.SetAdapter(new AzureDevOpsStepEvidence("transitionState", id, _targetState, "transitioned"));
                return OperationResult.Pass($"transitioned {id} to {_targetState}");
            }
            catch (HttpRequestException ex)
            {
                _evidence.SetAdapter(new AzureDevOpsStepEvidence("transitionState", id, _targetState, $"error: {ex.Message}"));
                return OperationResult.Fail(ex.Message);
            }
        }

        _evidence.SetAdapter(new AzureDevOpsStepEvidence("transitionState", null, _targetState, "no-work-item"));
        return OperationResult.Fail("no work item created in this case");
    }
}
