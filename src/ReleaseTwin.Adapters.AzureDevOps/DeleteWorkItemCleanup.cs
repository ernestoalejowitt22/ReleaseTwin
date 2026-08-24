using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>Moves the created work item to the recycle bin (Azure DevOps default delete behavior; not a permanent destroy).</summary>
internal sealed class DeleteWorkItemCleanup : ICleanupOperation
{
    private readonly AzureDevOpsClient _client;
    public DeleteWorkItemCleanup(AzureDevOpsClient client) => _client = client;

    public async Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("azdo.workItemId", out var idObj) && idObj is int id)
        {
            try
            {
                await _client.DeleteWorkItemAsync(id, cancellationToken);
                return new CleanupResult(true);
            }
            catch (HttpRequestException ex)
            {
                return new CleanupResult(false, ex.Message);
            }
        }

        return new CleanupResult(true);
    }
}
