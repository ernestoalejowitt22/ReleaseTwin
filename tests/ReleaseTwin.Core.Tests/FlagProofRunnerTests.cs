using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class FlagProofRunnerTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(IReadOnlyList<CapabilityRequirement>? requiredCapabilities = null) => new(
        "CLM-042",
        new OracleReference("tickets/CLM-042"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        new[] { new PipelineStep("checkDeductible") },
        Array.Empty<CleanupDeclaration>(),
        RequiredCapabilities: requiredCapabilities);

    private sealed class FeatureAwareOperation : IOperation
    {
        private readonly FakeFeatureStateController _controller;
        private readonly string _featureKey;

        public FeatureAwareOperation(FakeFeatureStateController controller, string featureKey)
        {
            _controller = controller;
            _featureKey = featureKey;
        }

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            Task.FromResult(_controller.IsEnabled(_featureKey) ? OperationResult.Pass() : OperationResult.Fail("deductible-wrong"));
    }

    private sealed class AlwaysPassOperation : IOperation
    {
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Pass());
    }

    private sealed class AlwaysFailOperation : IOperation
    {
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Fail("still-broken"));
    }

    private sealed class FakeFeatureStateController : IFeatureStateController
    {
        private readonly Dictionary<string, bool> _states = new();

        public Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken)
        {
            _states[featureKey] = enabled;
            return Task.CompletedTask;
        }

        public bool IsEnabled(string featureKey) => _states.TryGetValue(featureKey, out var enabled) && enabled;
    }

    private sealed class FakeCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
    {
        private readonly Dictionary<string, IOperation> _operations = new();
        private readonly HashSet<string> _capabilities = new();

        public FakeCatalog Operation(string name, IOperation operation)
        {
            _operations[name] = operation;
            return this;
        }

        public FakeCatalog Capability(string name)
        {
            _capabilities.Add(name);
            return this;
        }

        public bool TryGet(string name, out IOperation operation) => _operations.TryGetValue(name, out operation!);
        public bool TryGet(string name, out IPrerequisiteCheck check) { check = null!; return false; }
        public bool TryGet(string name, out ICleanupOperation operation) { operation = null!; return false; }
        public bool IsAvailable(string capabilityName) => _capabilities.Contains(capabilityName);
    }

    [Fact]
    public async Task SameFixtureAndBuildUsedForBothLegs()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog()
            .Operation("checkDeductible", new FeatureAwareOperation(controller, "claims-calc"))
            .Capability("flag-control:runtime");
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);
        var testCase = BuildCase();

        var result = await runner.RunAsync(testCase, "claims-calc", buildIdentity: "build-123");

        Assert.Equal("build-123", result.BuildIdentity);
        Assert.Equal(testCase.Fixture.ExpectedSha256, result.KnownBadLeg!.FixtureSha256);
        Assert.Equal(testCase.Fixture.ExpectedSha256, result.KnownGoodLeg!.FixtureSha256);
    }

    [Fact]
    public async Task ReportShowsOneReleaseProofOutcome()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog()
            .Operation("checkDeductible", new FeatureAwareOperation(controller, "claims-calc"))
            .Capability("flag-control:runtime");
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);

        var result = await runner.RunAsync(BuildCase(), "claims-calc", buildIdentity: "build-123");

        Assert.NotNull(result.KnownBadLeg);
        Assert.NotNull(result.KnownGoodLeg);
        Assert.True(Enum.IsDefined(typeof(FlagProofOutcome), result.Outcome));
    }

    [Fact]
    public async Task CorrectDiscriminationPasses()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog()
            .Operation("checkDeductible", new FeatureAwareOperation(controller, "claims-calc"))
            .Capability("flag-control:runtime");
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);

        var result = await runner.RunAsync(BuildCase(), "claims-calc", buildIdentity: "build-123");

        Assert.Equal(FlagProofOutcome.Passed, result.Outcome);
    }

    [Fact]
    public async Task BothLegsPassingIsWeakOracle()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog()
            .Operation("checkDeductible", new AlwaysPassOperation())
            .Capability("flag-control:runtime");
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);

        var result = await runner.RunAsync(BuildCase(), "claims-calc", buildIdentity: "build-123");

        Assert.Equal(FlagProofOutcome.WeakOracle, result.Outcome);
        Assert.NotEqual(FlagProofOutcome.Passed, result.Outcome);
    }

    [Fact]
    public async Task BothLegsFailingIsDistinctFromWeakOracle()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog()
            .Operation("checkDeductible", new AlwaysFailOperation())
            .Capability("flag-control:runtime");
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);

        var result = await runner.RunAsync(BuildCase(), "claims-calc", buildIdentity: "build-123");

        Assert.Equal(FlagProofOutcome.BothFailed, result.Outcome);
        Assert.NotEqual(FlagProofOutcome.WeakOracle, result.Outcome);
    }

    [Fact]
    public async Task MissingFeatureFlagControlDefersTheRun()
    {
        var controller = new FakeFeatureStateController();
        var catalog = new FakeCatalog().Operation("checkDeductible", new AlwaysPassOperation());
        // Deliberately no "flag-control:runtime" capability registered.
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var runner = new FlagProofRunner(executor, catalog, controller);

        var result = await runner.RunAsync(BuildCase(), "claims-calc", buildIdentity: "build-123");

        Assert.Equal(FlagProofOutcome.Ineligible, result.Outcome);
        Assert.Null(result.KnownBadLeg);
        Assert.Null(result.KnownGoodLeg);
    }
}
