using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class JourneyBranchingTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();

    private static TestCase BuildCase(params IPipelineEntry[] pipeline) => new(
        "CASE-B",
        new OracleReference("tickets/CASE-B"),
        new FixtureReference("f.json", FixtureHash, FixtureContent),
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        Array.Empty<CleanupDeclaration>());

    /// <summary>Records every invocation in a shared log; optionally captures a fixed value or fails.</summary>
    private sealed class LoggingOperation : IOperation
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly string? _captureValue;
        private readonly bool _succeeds;

        public LoggingOperation(List<string> log, string name, string? captureValue = null, bool succeeds = true)
        {
            _log = log;
            _name = name;
            _captureValue = captureValue;
            _succeeds = succeeds;
        }

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
        {
            _log.Add(_name);
            if (!_succeeds)
            {
                return Task.FromResult(OperationResult.Fail($"{_name}-failed"));
            }

            var produced = _captureValue is null ? null : captures.ToDictionary(c => c.Name, _ => _captureValue);
            return Task.FromResult(OperationResult.Pass(captures: produced));
        }
    }

    private sealed class FakeCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
    {
        private readonly Dictionary<string, IOperation> _operations = new();
        public FakeCatalog Operation(string name, IOperation operation) { _operations[name] = operation; return this; }
        public bool TryGet(string name, out IOperation operation) => _operations.TryGetValue(name, out operation!);
        public bool TryGet(string name, out IPrerequisiteCheck check) { check = null!; return false; }
        public bool TryGet(string name, out ICleanupOperation operation) { operation = null!; return false; }
        public bool IsAvailable(string capabilityName) => false;
    }

    private static (CaseExecutor Executor, List<string> Log) Build(string stateValue, bool thenFails = false)
    {
        var log = new List<string>();
        var catalog = new FakeCatalog()
            .Operation("read-state", new LoggingOperation(log, "read-state", captureValue: stateValue))
            .Operation("then-a", new LoggingOperation(log, "then-a", succeeds: !thenFails))
            .Operation("then-b", new LoggingOperation(log, "then-b"))
            .Operation("else-a", new LoggingOperation(log, "else-a"))
            .Operation("after", new LoggingOperation(log, "after"));
        return (new CaseExecutor(catalog, catalog, catalog, catalog), log);
    }

    private static TestCase PromoCase() => BuildCase(
        new PipelineStep("read-state", Capture: new[] { new CaptureDeclaration("promoState", "json:$.state") }),
        new ChoiceStep(
            new Condition("promoState", ConditionOperator.Equals, "applied"),
            new[] { new PipelineStep("then-a"), new PipelineStep("then-b") },
            new[] { new PipelineStep("else-a") }),
        new PipelineStep("after"));

    private static readonly ExecutionOptions WithEvidence = new() { CaptureEvidence = true };

    [Fact]
    public async Task TrueConditionRunsThenBranchAndRecordsElseAsNotExecuted()
    {
        var (executor, log) = Build("applied");

        var result = await executor.ExecuteAsync(PromoCase(), WithEvidence);

        Assert.True(result.Report.Passed);
        Assert.Equal(new[] { "read-state", "then-a", "then-b", "after" }, log);
        var steps = result.Evidence!.Steps;
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, steps.Select(s => s.Index));
        Assert.Equal(new[] { "read-state", "then-a", "then-b", "else-a", "after" }, steps.Select(s => s.OperationName));
        Assert.Equal(
            new[] { StepEvidenceOutcome.Passed, StepEvidenceOutcome.Passed, StepEvidenceOutcome.Passed, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.Passed },
            steps.Select(s => s.Outcome));
    }

    [Fact]
    public async Task FalseConditionRunsElseBranchAndRecordsThenAsNotExecuted()
    {
        var (executor, log) = Build("pending");

        var result = await executor.ExecuteAsync(PromoCase(), WithEvidence);

        Assert.True(result.Report.Passed);
        Assert.Equal(new[] { "read-state", "else-a", "after" }, log);
        Assert.Equal(
            new[] { StepEvidenceOutcome.Passed, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.Passed, StepEvidenceOutcome.Passed },
            result.Evidence!.Steps.Select(s => s.Outcome));
    }

    [Fact]
    public async Task RunsTakingOppositeBranchesRecordTheSameStepPositions()
    {
        var (thenExecutor, _) = Build("applied");
        var (elseExecutor, _) = Build("pending");

        var thenRun = await thenExecutor.ExecuteAsync(PromoCase(), WithEvidence);
        var elseRun = await elseExecutor.ExecuteAsync(PromoCase(), WithEvidence);

        Assert.Equal(
            thenRun.Evidence!.Steps.Select(s => (s.Index, s.OperationName)),
            elseRun.Evidence!.Steps.Select(s => (s.Index, s.OperationName)));
    }

    [Fact]
    public async Task AFailureInsideTheTakenBranchHaltsAndMarksEverythingAfterNotExecuted()
    {
        var (executor, log) = Build("applied", thenFails: true);

        var result = await executor.ExecuteAsync(PromoCase(), WithEvidence);

        Assert.False(result.Report.Passed);
        Assert.Equal(FailureClassification.Product, result.Report.Classification);
        Assert.Equal("then-a-failed", result.Report.FailureDetail);
        Assert.Equal(new[] { "read-state", "then-a" }, log);
        Assert.Equal(
            new[] { StepEvidenceOutcome.Passed, StepEvidenceOutcome.Failed, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.NotExecuted },
            result.Evidence!.Steps.Select(s => s.Outcome));
    }

    [Fact]
    public async Task AGuardedOffStepRecordsNotExecutedAndTheRunContinues()
    {
        var (executor, log) = Build("pending");
        var testCase = BuildCase(
            new PipelineStep("read-state", Capture: new[] { new CaptureDeclaration("promoState", "json:$.state") }),
            new PipelineStep("then-a", When: new Condition("promoState", ConditionOperator.Equals, "applied")),
            new PipelineStep("after"));

        var result = await executor.ExecuteAsync(testCase, WithEvidence);

        Assert.True(result.Report.Passed);
        Assert.Equal(new[] { "read-state", "after" }, log);
        Assert.Equal(
            new[] { StepEvidenceOutcome.Passed, StepEvidenceOutcome.NotExecuted, StepEvidenceOutcome.Passed },
            result.Evidence!.Steps.Select(s => s.Outcome));
    }

    [Fact]
    public async Task AGuardedOnStepRunsNormally()
    {
        var (executor, log) = Build("applied");
        var testCase = BuildCase(
            new PipelineStep("read-state", Capture: new[] { new CaptureDeclaration("promoState", "json:$.state") }),
            new PipelineStep("then-a", When: new Condition("promoState", ConditionOperator.Exists)));

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
        Assert.Equal(new[] { "read-state", "then-a" }, log);
    }

    [Fact]
    public async Task AChoiceWithAnEmptyTakenBranchContinuesAfterTheJoin()
    {
        var (executor, log) = Build("pending");
        var testCase = BuildCase(
            new PipelineStep("read-state", Capture: new[] { new CaptureDeclaration("promoState", "json:$.state") }),
            new ChoiceStep(new Condition("promoState", ConditionOperator.Equals, "applied"), new[] { new PipelineStep("then-a") }, Array.Empty<PipelineStep>()),
            new PipelineStep("after"));

        var result = await executor.ExecuteAsync(testCase, WithEvidence);

        Assert.True(result.Report.Passed);
        Assert.Equal(new[] { "read-state", "after" }, log);
        Assert.Equal(3, result.Evidence!.Steps.Count);
    }

    [Fact]
    public async Task AnUnknownOperationInsideABranchIsStillRejectedUpFront()
    {
        var (executor, log) = Build("pending");
        var testCase = BuildCase(
            new PipelineStep("read-state", Capture: new[] { new CaptureDeclaration("promoState", "json:$.state") }),
            new ChoiceStep(new Condition("promoState", ConditionOperator.Exists), new[] { new PipelineStep("no-such-op") }, Array.Empty<PipelineStep>()));

        await Assert.ThrowsAsync<UnknownReferenceException>(() => executor.ExecuteAsync(testCase));
        Assert.Empty(log);
    }

    [Fact]
    public async Task AFixtureMismatchRecordsBranchStepsAsNotExecuted()
    {
        var (executor, _) = Build("applied");
        var bad = PromoCase() with { Fixture = new FixtureReference("f.json", new string('0', 64), FixtureContent) };

        var result = await executor.ExecuteAsync(bad, WithEvidence);

        Assert.Equal(5, result.Evidence!.Steps.Count);
        Assert.All(result.Evidence.Steps, s => Assert.Equal(StepEvidenceOutcome.NotExecuted, s.Outcome));
    }

    [Theory]
    [InlineData(ConditionOperator.Equals, "applied", "applied", true)]
    [InlineData(ConditionOperator.Equals, "applied", "pending", false)]
    [InlineData(ConditionOperator.Equals, "applied", null, false)]
    [InlineData(ConditionOperator.NotEquals, "applied", "pending", true)]
    [InlineData(ConditionOperator.NotEquals, "applied", "applied", false)]
    [InlineData(ConditionOperator.NotEquals, "applied", null, true)]
    [InlineData(ConditionOperator.Exists, null, "x", true)]
    [InlineData(ConditionOperator.Exists, null, "", false)]
    [InlineData(ConditionOperator.Exists, null, null, false)]
    [InlineData(ConditionOperator.NotExists, null, null, true)]
    [InlineData(ConditionOperator.NotExists, null, "", true)]
    [InlineData(ConditionOperator.NotExists, null, "x", false)]
    public void ConditionEvaluatorTreatsAnAbsentCaptureAsAbsent(ConditionOperator op, string? value, string? captured, bool expected)
    {
        var captures = new Dictionary<string, string>();
        if (captured is not null)
        {
            captures["s"] = captured;
        }

        Assert.Equal(expected, ConditionEvaluator.Evaluate(new Condition("s", op, value), captures));
    }

    [Fact]
    public void FlattenStepsOrdersThenBeforeElse()
    {
        var flat = PromoCase().Pipeline.FlattenSteps();

        Assert.Equal(new[] { "read-state", "then-a", "then-b", "else-a", "after" }, flat.Select(s => s.OperationName));
    }
}
