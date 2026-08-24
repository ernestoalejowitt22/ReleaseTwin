using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.AdapterSdk.Tests;

public class CompositionRootTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(
        string caseId = "CASE-1",
        IReadOnlyList<PipelineStep>? pipeline = null,
        IReadOnlyList<CapabilityRequirement>? requiredCapabilities = null) =>
        new(
            caseId,
            new OracleReference($"tickets/{caseId}"),
            ValidFixture,
            Array.Empty<PrerequisiteDeclaration>(),
            pipeline ?? Array.Empty<PipelineStep>(),
            Array.Empty<CleanupDeclaration>(),
            RequiredCapabilities: requiredCapabilities);

    private sealed class NamedOperation : IOperation
    {
        public string Name { get; }
        public NamedOperation(string name) => Name = name;
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Pass(Name));
    }

    private sealed class SingleOperationAdapter : IAdapterModule
    {
        private readonly string _operationName;
        public string Name { get; }

        public SingleOperationAdapter(string name, string operationName)
        {
            Name = name;
            _operationName = operationName;
        }

        public void Register(IAdapterRegistrationBuilder builder) =>
            builder.AddOperation(_operationName, new NamedOperation(_operationName));
    }

    private sealed class CapabilityAdapter : IAdapterModule
    {
        private readonly string _capabilityName;
        public string Name => "capability-adapter";
        public CapabilityAdapter(string capabilityName) => _capabilityName = capabilityName;
        public void Register(IAdapterRegistrationBuilder builder) => builder.AddCapability(_capabilityName);
    }

    [Fact]
    public async Task CoreStartsWithZeroAdaptersInstalled()
    {
        var root = new CompositionRoot();
        var executor = root.BuildExecutor();

        var testCase = BuildCase();

        await Assert.ThrowsAsync<UnknownReferenceException>(async () =>
        {
            var caseWithOp = BuildCase(pipeline: new[] { new PipelineStep("anything") });
            await executor.ExecuteAsync(caseWithOp);
        });

        // With no operations declared at all, a case with an empty pipeline still executes cleanly,
        // proving the composition is usable (just empty) rather than broken.
        var report = await executor.ExecuteAsync(testCase);
        Assert.True(report.Passed);
    }

    [Fact]
    public async Task NewAdapterInstallsWithoutCoreChanges()
    {
        var root = new CompositionRoot();
        root.Install(new SingleOperationAdapter("adapter-a", "a.op"));
        var executor = root.BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase(pipeline: new[] { new PipelineStep("a.op") }));

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task TwoAdaptersInstalledTogetherBothAvailable()
    {
        var root = new CompositionRoot();
        root.Install(new SingleOperationAdapter("adapter-a", "a.op"));
        root.Install(new SingleOperationAdapter("adapter-b", "b.op"));
        var executor = root.BuildExecutor();

        var reportA = await executor.ExecuteAsync(BuildCase("A", new[] { new PipelineStep("a.op") }));
        var reportB = await executor.ExecuteAsync(BuildCase("B", new[] { new PipelineStep("b.op") }));

        Assert.True(reportA.Passed);
        Assert.True(reportB.Passed);
    }

    [Fact]
    public async Task CaseReferencingUnregisteredOperationReportsErrorBeforeExecution()
    {
        var root = new CompositionRoot();
        root.Install(new SingleOperationAdapter("adapter-a", "a.op"));
        var executor = root.BuildExecutor();

        var testCase = BuildCase(pipeline: new[] { new PipelineStep("not-registered") });

        var exception = await Assert.ThrowsAsync<UnknownReferenceException>(() => executor.ExecuteAsync(testCase));
        Assert.Contains("not-registered", exception.Message);
    }

    [Fact]
    public async Task MissingRequiredCapabilityIsDistinctConfigurationResult()
    {
        var root = new CompositionRoot();
        root.Install(new SingleOperationAdapter("adapter-a", "a.op"));
        var executor = root.BuildExecutor();

        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("a.op") },
            requiredCapabilities: new[] { new CapabilityRequirement("browser:chromium") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.NotEqual(FailureClassification.Product, report.Classification);
    }

    [Fact]
    public async Task DeclaredCapabilitySatisfiesRequirement()
    {
        var root = new CompositionRoot();
        root.Install(new SingleOperationAdapter("adapter-a", "a.op"));
        root.Install(new CapabilityAdapter("browser:chromium"));
        var executor = root.BuildExecutor();

        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("a.op") },
            requiredCapabilities: new[] { new CapabilityRequirement("browser:chromium") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
    }
}
