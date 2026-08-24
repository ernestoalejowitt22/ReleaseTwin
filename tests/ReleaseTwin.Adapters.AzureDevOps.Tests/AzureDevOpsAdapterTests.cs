using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.AzureDevOps.Tests;

public class AzureDevOpsAdapterTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static AzureDevOpsOptions TestOptions() =>
        new(Organization: "test-org", Project: "TeamProject", PersonalAccessToken: Environment.GetEnvironmentVariable("TEST_PAT") ?? "test-pat-from-env");

    // Task 2.2: constructing with a PAT sourced from a variable, not a literal.
    [Fact]
    public void AdapterIsConstructedWithExternallySuppliedCredential()
    {
        var pat = Environment.GetEnvironmentVariable("TEST_PAT") ?? Guid.NewGuid().ToString();
        var options = new AzureDevOpsOptions("test-org", "TeamProject", pat);

        using var adapter = new AzureDevOpsAdapter(options, "TeamProject\\Area", variableGroupId: 1, handler: new FakeAzureDevOpsHandler());

        Assert.Equal("azure-devops", adapter.Name);
    }

    // Task 2.2: no credential literal exists anywhere in the adapter's source.
    [Fact]
    public void AdapterSourceContainsNoCredentialLiteral()
    {
        var srcDir = FindSourceDirectory();
        var suspiciousPatterns = new[] { "pat = \"", "PersonalAccessToken = \"", "token = \"", "apikey = \"" };

        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file).ToLowerInvariant();
            foreach (var pattern in suspiciousPatterns)
            {
                Assert.DoesNotContain(pattern.ToLowerInvariant(), content);
            }
        }
    }

    private static string FindSourceDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ReleaseTwin.Adapters.AzureDevOps")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Could not locate src/ReleaseTwin.Adapters.AzureDevOps from test output directory.")
            : Path.Combine(dir.FullName, "src", "ReleaseTwin.Adapters.AzureDevOps");
    }

    private static TestCase BuildCase(string caseId, IReadOnlyList<PipelineStep> pipeline, ResourceKey? resourceKey = null) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        new[] { new PrerequisiteDeclaration("azdo.areaPathExists", "release-proof owner") },
        pipeline,
        new[] { new CleanupDeclaration("azdo.deleteWorkItem") },
        resourceKey);

    private static CaseExecutor BuildExecutor(AzureDevOpsAdapter adapter)
    {
        var root = new CompositionRoot();
        root.Install(adapter);
        return root.BuildExecutor();
    }

    [Fact]
    public async Task WorkItemLifecycleRunsEndToEnd()
    {
        var handler = new FakeAzureDevOpsHandler();
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\Area", variableGroupId: 1, handler: handler);
        var executor = BuildExecutor(adapter);

        var testCase = BuildCase("AZDO-1", new[]
        {
            new PipelineStep("azdo.createWorkItem"),
            new PipelineStep("azdo.getWorkItem"),
            new PipelineStep("azdo.transitionWorkItemState"),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public async Task MissingAreaPathIsPrerequisiteFailure()
    {
        var handler = new FakeAzureDevOpsHandler();
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\DoesNotExist", variableGroupId: 1, handler: handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase("AZDO-2", new[] { new PipelineStep("azdo.createWorkItem") }));

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Prerequisite, report.Classification);
    }

    // Task 4.1: shared area path as ResourceKey serializes concurrent work item creation.
    [Fact]
    public async Task SharedAreaPathResourceKeySerializesWorkItemCreation()
    {
        var handler = new FakeAzureDevOpsHandler { CreateDelay = TimeSpan.FromMilliseconds(50) };
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\Area", variableGroupId: 1, handler: handler);
        var executor = BuildExecutor(adapter);
        var resourceKey = new ResourceKey("TeamProject\\Area");

        var caseA = BuildCase("AZDO-A", new[] { new PipelineStep("azdo.createWorkItem") }, resourceKey);
        var caseB = BuildCase("AZDO-B", new[] { new PipelineStep("azdo.createWorkItem") }, resourceKey);

        await Task.WhenAll(executor.ExecuteAsync(caseA), executor.ExecuteAsync(caseB));

        Assert.Equal(1, handler.MaxConcurrentCreates);
    }

    // Task 4.4: FlagProofRunner end to end against the (faked) real Azure DevOps variable group state.
    [Fact]
    public async Task FlagProofRunsEndToEndAgainstVariableGroup()
    {
        var handler = new FakeAzureDevOpsHandler();
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\Area", variableGroupId: 1, variableName: "claims-calc", handler: handler);
        var executor = BuildExecutor(adapter);
        var runner = new FlagProofRunner(executor, new CompositionRootCapabilityAdapter(adapter), adapter.FeatureStateController);

        var testCase = BuildCase("AZDO-FLAG", new[] { new PipelineStep("azdo.readFeatureVariable") });

        var result = await runner.RunAsync(testCase, "claims-calc", buildIdentity: "build-42");

        Assert.Equal(FlagProofOutcome.Passed, result.Outcome);
    }

    // Task 5.3: Gap 1 fix — an unreachable/unauthorized check (Inconclusive) and a confirmed
    // "does not exist" (NotSatisfied) must now report distinct classifications.
    [Fact]
    public async Task AreaPathCheckFailureIsNowDistinguishableFromNotFound_Gap1Fixed()
    {
        var handler = new FakeAzureDevOpsHandler { SimulateAreaPathCheckFailure = true };
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\Area", variableGroupId: 1, handler: handler);
        var executor = BuildExecutor(adapter);

        var failureReport = await executor.ExecuteAsync(BuildCase("AZDO-FAIL", new[] { new PipelineStep("azdo.createWorkItem") }));

        var notFoundHandler = new FakeAzureDevOpsHandler();
        using var notFoundAdapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\DoesNotExist", variableGroupId: 1, handler: notFoundHandler);
        var notFoundExecutor = BuildExecutor(notFoundAdapter);
        var notFoundReport = await notFoundExecutor.ExecuteAsync(BuildCase("AZDO-NOTFOUND", new[] { new PipelineStep("azdo.createWorkItem") }));

        // A real 401 (couldn't check -> Inconclusive -> Infrastructure) and a real 404 (confirmed
        // absent -> NotSatisfied -> Prerequisite) now report different classifications.
        Assert.NotEqual(failureReport.Classification, notFoundReport.Classification);
        Assert.Equal(FailureClassification.Infrastructure, failureReport.Classification);
        Assert.Equal(FailureClassification.Prerequisite, notFoundReport.Classification);
    }

    // graceful-capability-gating task 2.2: no adapter instance needed to read the manifest.
    [Fact]
    public void KnownOperationCapabilitiesIsAccessibleWithoutConstructingAnAdapter()
    {
        Assert.Equal("http:azure-devops", AzureDevOpsAdapter.KnownOperationCapabilities["azdo.createWorkItem"]);
        Assert.True(AzureDevOpsAdapter.KnownOperationCapabilities.Values.All(v => v == "http:azure-devops"));
    }

    // graceful-capability-gating task 2.3: the manifest can't silently drift from what Register()
    // actually contributes to the catalog.
    [Fact]
    public void KnownOperationCapabilitiesMatchesWhatRegisterContributes()
    {
        using var adapter = new AzureDevOpsAdapter(TestOptions(), "TeamProject\\Area", variableGroupId: 1, handler: new FakeAzureDevOpsHandler());
        var root = new CompositionRoot();
        root.Install(adapter);
        var catalog = root.Catalog;

        var registeredNames = new HashSet<string>();
        foreach (var name in AzureDevOpsAdapter.KnownOperationCapabilities.Keys)
        {
            var isOperation = catalog.TryGet(name, out IOperation _);
            var isPrerequisite = catalog.TryGet(name, out IPrerequisiteCheck _);
            var isCleanup = catalog.TryGet(name, out ICleanupOperation _);
            Assert.True(isOperation || isPrerequisite || isCleanup, $"'{name}' is in the manifest but Register() never contributed it");
            registeredNames.Add(name);
        }

        Assert.Equal(new[] { "azdo.areaPathExists", "azdo.createWorkItem", "azdo.deleteWorkItem", "azdo.getWorkItem", "azdo.readFeatureVariable", "azdo.transitionWorkItemState" }, registeredNames.OrderBy(n => n));
    }

    private sealed class CompositionRootCapabilityAdapter : ICapabilityCatalog
    {
        private readonly AzureDevOpsAdapter _adapter;
        public CompositionRootCapabilityAdapter(AzureDevOpsAdapter adapter) => _adapter = adapter;
        public bool IsAvailable(string capabilityName) => capabilityName == "flag-control:runtime";
    }
}
