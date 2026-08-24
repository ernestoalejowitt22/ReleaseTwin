using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>
/// tasks.md 5.3: escalated from the boolean PrerequisiteResult once the fake-handler test confirmed
/// a real misclassification (an unreachable/unauthorized call and a confirmed "does not exist" both
/// reported as the same failure). Now reports Inconclusive distinctly from NotSatisfied.
/// </summary>
internal sealed class AreaPathExistsCheck : IPrerequisiteCheck
{
    private readonly AzureDevOpsClient _client;
    private readonly string _areaPath;

    public AreaPathExistsCheck(AzureDevOpsClient client, string areaPath)
    {
        _client = client;
        _areaPath = areaPath;
    }

    public async Task<PrerequisiteResult> EvaluateAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _client.AreaPathExistsAsync(_areaPath, cancellationToken);
            return exists
                ? PrerequisiteResult.Satisfied()
                : PrerequisiteResult.NotSatisfied($"area path '{_areaPath}' does not exist");
        }
        catch (HttpRequestException ex)
        {
            return PrerequisiteResult.Inconclusive($"area path check could not be completed: {ex.Message}");
        }
    }
}
