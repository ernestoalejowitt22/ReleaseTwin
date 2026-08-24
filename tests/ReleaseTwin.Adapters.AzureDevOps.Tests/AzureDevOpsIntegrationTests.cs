using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps.Tests;

/// <summary>
/// tasks.md 6.1/6.2: exercises the adapter against the real Azure DevOps API. Scaffolded now, run
/// later — every test skips (no-ops, with an explanatory message) unless AZDO_ORG, AZDO_PROJECT,
/// AZDO_PAT, and AZDO_AREA_PATH are all set. No sandbox org/PAT is required to have this code exist
/// or to keep the rest of the suite green; standing one up is a separate, later step (see
/// docs/installation-model.md — this project doesn't need a hosted org until someone runs these).
///
/// Filter to just these with: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class AzureDevOpsIntegrationTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static bool TryGetLiveCredentials(out AzureDevOpsOptions options, out string areaPath)
    {
        var org = Environment.GetEnvironmentVariable("AZDO_ORG");
        var project = Environment.GetEnvironmentVariable("AZDO_PROJECT");
        var pat = Environment.GetEnvironmentVariable("AZDO_PAT");
        areaPath = Environment.GetEnvironmentVariable("AZDO_AREA_PATH") ?? "";

        if (string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(project)
            || string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(areaPath))
        {
            options = null!;
            return false;
        }

        options = new AzureDevOpsOptions(org, project, pat);
        return true;
    }

    private static TestCase BuildCase(string caseId, IReadOnlyList<PipelineStep> pipeline) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        new[] { new PrerequisiteDeclaration("azdo.areaPathExists", "release-proof owner") },
        pipeline,
        new[] { new CleanupDeclaration("azdo.deleteWorkItem") });

    [Fact]
    public async Task CreateReadTransitionCleanup_AgainstRealAzureDevOps()
    {
        if (!TryGetLiveCredentials(out var options, out var areaPath))
        {
            // No sandbox credentials configured — this is expected until someone sets one up.
            // See docs/installation-model.md and design.md's Open Questions.
            return;
        }

        using var adapter = new AzureDevOpsAdapter(options, areaPath, variableGroupId: 1);
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("AZDO-INTEGRATION-1", new[]
        {
            new PipelineStep("azdo.createWorkItem"),
            new PipelineStep("azdo.getWorkItem"),
            new PipelineStep("azdo.transitionWorkItemState"),
        }));

        Assert.True(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public async Task AreaPathCheckAgainstUnreachableOrg_RealNetworkFailure()
    {
        if (!TryGetLiveCredentials(out _, out var areaPath))
        {
            return;
        }

        // Deliberately wrong org: a real DNS/auth failure, not a simulated one — the actual
        // signal task 5.2 asked for, once this can run.
        var badOptions = new AzureDevOpsOptions("this-org-should-not-exist-releasetwin", "TeamProject", "invalid-pat");
        using var adapter = new AzureDevOpsAdapter(badOptions, areaPath, variableGroupId: 1);
        var root = new CompositionRoot();
        root.Install(adapter);
        var executor = root.BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("AZDO-INTEGRATION-2", new[] { new PipelineStep("azdo.createWorkItem") }));

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Prerequisite, report.Classification);
    }
}
