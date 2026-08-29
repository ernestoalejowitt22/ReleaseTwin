using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class RunEvidenceTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(params PipelineStep[] pipeline) => new(
        "CASE-1",
        new OracleReference("tickets/CASE-1"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        Array.Empty<CleanupDeclaration>());

    private sealed class StubOp : IOperation
    {
        private readonly bool _passes;
        public StubOp(bool passes) => _passes = passes;
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken) =>
            Task.FromResult(_passes ? OperationResult.Pass() : OperationResult.Fail("mismatch"));
    }

    private sealed class EmittingOp : IOperation, IEvidenceEmittingOperation
    {
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Fail("expected 'a' but got 'b'"));

        public EvidenceContribution? DrainEvidence() =>
            new(new AssertionDetail("$.status", "a", "b"), new { note = "adapter-specific" });
    }

    private sealed class FakeCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
    {
        private readonly Dictionary<string, IOperation> _operations = new();
        public FakeCatalog Operation(string name, IOperation op) { _operations[name] = op; return this; }
        public bool TryGet(string name, out IOperation operation) => _operations.TryGetValue(name, out operation!);
        public bool TryGet(string name, out IPrerequisiteCheck check) { check = null!; return false; }
        public bool TryGet(string name, out ICleanupOperation operation) { operation = null!; return false; }
        public bool IsAvailable(string capabilityName) => false;
    }

    private static CaseExecutor Executor(FakeCatalog c) => new(c, c, c, c);

    [Fact]
    public async Task CaptureOff_ProducesIdenticalReport_AndNoEvidence()
    {
        var catalog = new FakeCatalog().Operation("a", new StubOp(true)).Operation("b", new StubOp(false));
        var testCase = BuildCase(new PipelineStep("a"), new PipelineStep("b"));

        var reportOnly = await Executor(catalog).ExecuteAsync(testCase);
        var withOptions = await Executor(catalog).ExecuteAsync(testCase, ExecutionOptions.Default);

        Assert.Null(withOptions.Evidence);
        Assert.Equal(reportOnly.CaseId, withOptions.Report.CaseId);
        Assert.Equal(reportOnly.Passed, withOptions.Report.Passed);
        Assert.Equal(reportOnly.Classification, withOptions.Report.Classification);
        Assert.Equal(reportOnly.FailureDetail, withOptions.Report.FailureDetail);
        Assert.Equal(reportOnly.CleanupStatus, withOptions.Report.CleanupStatus);
    }

    [Fact]
    public async Task CaptureOn_RecordsOrderedSteps_AndMarksPostHaltNotExecuted()
    {
        var catalog = new FakeCatalog()
            .Operation("a", new StubOp(true))
            .Operation("b", new StubOp(false))
            .Operation("c", new StubOp(true));
        var testCase = BuildCase(new PipelineStep("a"), new PipelineStep("b"), new PipelineStep("c"));

        var result = await Executor(catalog).ExecuteAsync(testCase, new ExecutionOptions { CaptureEvidence = true });

        Assert.NotNull(result.Evidence);
        Assert.Equal("CASE-1", result.Evidence!.CaseId);
        Assert.Equal("tickets/CASE-1", result.Evidence.OracleLocator);
        Assert.Collection(result.Evidence.Steps,
            s => { Assert.Equal(0, s.Index); Assert.Equal("a", s.OperationName); Assert.Equal(StepEvidenceOutcome.Passed, s.Outcome); },
            s => { Assert.Equal(1, s.Index); Assert.Equal("b", s.OperationName); Assert.Equal(StepEvidenceOutcome.Failed, s.Outcome); },
            s => { Assert.Equal(2, s.Index); Assert.Equal(StepEvidenceOutcome.NotExecuted, s.Outcome); });
    }

    [Fact]
    public async Task CaptureOn_DrainsAssertionDetailAndOpaqueAdapterEvidence()
    {
        var catalog = new FakeCatalog().Operation("assert", new EmittingOp());
        var testCase = BuildCase(new PipelineStep("assert"));

        var result = await Executor(catalog).ExecuteAsync(testCase, new ExecutionOptions { CaptureEvidence = true });

        var step = Assert.Single(result.Evidence!.Steps);
        Assert.Equal("$.status", step.Assertion!.Expression);
        Assert.Equal("a", step.Assertion.Expected);
        Assert.Equal("b", step.Assertion.Observed);
        Assert.NotNull(step.AdapterEvidence);
    }

    [Fact]
    public async Task FlagProof_CaptureOn_ProducesTwoLabelledLegs()
    {
        var catalog = new FakeCatalog().Operation("x", new StubOp(true));
        var featureController = new StubFeatureController();
        var testCase = BuildCase(new PipelineStep("x"));

        var permissive = new PermissiveCatalog(catalog);
        var runner2 = new FlagProofRunner(new CaseExecutor(permissive, permissive, permissive, permissive), permissive, featureController);

        var result = await runner2.RunAsync(testCase, "flag", "build-1", new ExecutionOptions { CaptureEvidence = true });

        Assert.NotNull(result.KnownBadEvidence);
        Assert.NotNull(result.KnownGoodEvidence);
        Assert.Equal("known-bad", result.KnownBadEvidence!.Leg);
        Assert.Equal("known-good", result.KnownGoodEvidence!.Leg);
    }

    private sealed class StubFeatureController : IFeatureStateController
    {
        public Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PermissiveCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
    {
        private readonly FakeCatalog _inner;
        public PermissiveCatalog(FakeCatalog inner) => _inner = inner;
        public bool TryGet(string name, out IOperation operation) => _inner.TryGet(name, out operation);
        public bool TryGet(string name, out IPrerequisiteCheck check) { check = null!; return false; }
        public bool TryGet(string name, out ICleanupOperation operation) { operation = null!; return false; }
        public bool IsAvailable(string capabilityName) => true;
    }
}
