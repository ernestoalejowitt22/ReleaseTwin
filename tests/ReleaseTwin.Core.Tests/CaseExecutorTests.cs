using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class CaseExecutorTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");

    private static string FixtureHash =>
        Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();

    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(
        IReadOnlyList<PrerequisiteDeclaration>? prerequisites = null,
        IReadOnlyList<PipelineStep>? pipeline = null,
        IReadOnlyList<CleanupDeclaration>? cleanup = null,
        FixtureReference? fixture = null,
        ResourceKey? resourceKey = null,
        IReadOnlyList<CapabilityRequirement>? requiredCapabilities = null) =>
        new(
            "CASE-1",
            new OracleReference("tickets/CASE-1"),
            fixture ?? ValidFixture,
            prerequisites ?? Array.Empty<PrerequisiteDeclaration>(),
            pipeline ?? Array.Empty<PipelineStep>(),
            cleanup ?? Array.Empty<CleanupDeclaration>(),
            resourceKey,
            requiredCapabilities);

    private sealed class StubOperation : IOperation
    {
        private readonly Func<int, OperationResult> _behavior;
        public int Invocations { get; private set; }

        public StubOperation(Func<int, OperationResult> behavior) => _behavior = behavior;

        public static StubOperation AlwaysPass() => new(_ => OperationResult.Pass());
        public static StubOperation AlwaysFail(string detail = "assertion-mismatch") => new(_ => OperationResult.Fail(detail));

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(_behavior(Invocations));
        }
    }

    private sealed class DelayOperation : IOperation
    {
        private readonly TimeSpan _delay;
        public DelayOperation(TimeSpan delay) => _delay = delay;

        public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return OperationResult.Pass();
        }
    }

    private sealed class StubPrerequisite : IPrerequisiteCheck
    {
        private readonly PrerequisiteResult _result;
        public StubPrerequisite(bool passed, string? detail = null) =>
            _result = passed ? PrerequisiteResult.Satisfied(detail) : PrerequisiteResult.NotSatisfied(detail);
        public Task<PrerequisiteResult> EvaluateAsync(CaseExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private sealed class StubCleanup : ICleanupOperation
    {
        private readonly bool _succeeds;
        public int Invocations { get; private set; }
        public StubCleanup(bool succeeds = true) => _succeeds = succeeds;

        public Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(new CleanupResult(_succeeds));
        }
    }

    private sealed class FakeCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
    {
        private readonly Dictionary<string, IOperation> _operations = new();
        private readonly Dictionary<string, IPrerequisiteCheck> _prerequisites = new();
        private readonly Dictionary<string, ICleanupOperation> _cleanups = new();
        private readonly HashSet<string> _capabilities = new();

        public FakeCatalog Operation(string name, IOperation operation)
        {
            _operations[name] = operation;
            return this;
        }

        public FakeCatalog Prerequisite(string name, IPrerequisiteCheck check)
        {
            _prerequisites[name] = check;
            return this;
        }

        public FakeCatalog Cleanup(string name, ICleanupOperation operation)
        {
            _cleanups[name] = operation;
            return this;
        }

        public FakeCatalog Capability(string name)
        {
            _capabilities.Add(name);
            return this;
        }

        public bool TryGet(string name, out IOperation operation) => _operations.TryGetValue(name, out operation!);
        public bool TryGet(string name, out IPrerequisiteCheck check) => _prerequisites.TryGetValue(name, out check!);
        public bool TryGet(string name, out ICleanupOperation operation) => _cleanups.TryGetValue(name, out operation!);
        public bool IsAvailable(string capabilityName) => _capabilities.Contains(capabilityName);
    }

    private static CaseExecutor BuildExecutor(FakeCatalog catalog) => new(catalog, catalog, catalog, catalog);

    [Fact]
    public async Task ReportIncludesOracleReference()
    {
        var catalog = new FakeCatalog();
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase();

        var report = await executor.ExecuteAsync(testCase);

        Assert.Equal(testCase.CaseId, report.CaseId);
        Assert.Equal(testCase.Oracle, report.Oracle);
    }

    [Fact]
    public async Task VerifiedFixturePassesThrough()
    {
        var catalog = new FakeCatalog();
        var executor = BuildExecutor(catalog);

        var report = await executor.ExecuteAsync(BuildCase());

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task TamperedFixtureBlocksExecution()
    {
        var catalog = new FakeCatalog().Operation("op", StubOperation.AlwaysPass());
        var executor = BuildExecutor(catalog);
        var badFixture = new FixtureReference("fixtures/case.json", "0000000000000000000000000000000000000000000000000000000000000000", FixtureContent);
        var op = StubOperation.AlwaysPass();
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("op") }, fixture: badFixture);
        catalog.Operation("op", op);

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Infrastructure, report.Classification);
        Assert.Equal(0, op.Invocations);
    }

    [Fact]
    public async Task FailingPrerequisiteHaltsPipelineAndIsClassifiedSeparately()
    {
        var op = StubOperation.AlwaysPass();
        var catalog = new FakeCatalog()
            .Prerequisite("pre", new StubPrerequisite(passed: false, detail: "policy missing"))
            .Operation("op", op);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            prerequisites: new[] { new PrerequisiteDeclaration("pre", "QA") },
            pipeline: new[] { new PipelineStep("op") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Prerequisite, report.Classification);
        Assert.Equal(0, op.Invocations);
    }

    [Fact]
    public async Task PassingPrerequisitesAllowExecutionToProceed()
    {
        var op = StubOperation.AlwaysPass();
        var catalog = new FakeCatalog()
            .Prerequisite("pre", new StubPrerequisite(passed: true))
            .Operation("op", op);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            prerequisites: new[] { new PrerequisiteDeclaration("pre", "QA") },
            pipeline: new[] { new PipelineStep("op") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
        Assert.Equal(1, op.Invocations);
    }

    [Fact]
    public async Task OperationsRunInDeclaredOrder()
    {
        var order = new List<string>();
        IOperation Recording(string name) => new RecordingOperation(name, order);

        var catalog = new FakeCatalog()
            .Operation("a", Recording("a"))
            .Operation("b", Recording("b"))
            .Operation("c", Recording("c"));
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("a"), new PipelineStep("b"), new PipelineStep("c") });

        await executor.ExecuteAsync(testCase);

        Assert.Equal(new[] { "a", "b", "c" }, order);
    }

    private sealed class RecordingOperation : IOperation
    {
        private readonly string _name;
        private readonly List<string> _order;
        public RecordingOperation(string name, List<string> order) { _name = name; _order = order; }
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            _order.Add(_name);
            return Task.FromResult(OperationResult.Pass());
        }
    }

    [Fact]
    public async Task UnexpectedOperationFailureStopsThePipeline()
    {
        var b = StubOperation.AlwaysFail();
        var c = StubOperation.AlwaysPass();
        var catalog = new FakeCatalog()
            .Operation("a", StubOperation.AlwaysPass())
            .Operation("b", b)
            .Operation("c", c);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("a"), new PipelineStep("b"), new PipelineStep("c") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(0, c.Invocations);
    }

    [Fact]
    public async Task ExpectedFailureIsReportedAsPass()
    {
        var catalog = new FakeCatalog().Operation("op", StubOperation.AlwaysFail());
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("op", ExpectFailure: true) });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task UnexpectedPassIsReportedAsFailureWithDistinctClassification()
    {
        var catalog = new FakeCatalog().Operation("op", StubOperation.AlwaysPass());
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("op", ExpectFailure: true) });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.NotEqual(FailureClassification.Product, report.Classification);
    }

    [Fact]
    public async Task CleanupRunsAfterPipelineFailure()
    {
        var cleanup = new StubCleanup();
        var catalog = new FakeCatalog()
            .Operation("op", StubOperation.AlwaysFail())
            .Cleanup("cleanup", cleanup);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("op") },
            cleanup: new[] { new CleanupDeclaration("cleanup") });

        await executor.ExecuteAsync(testCase);

        Assert.Equal(1, cleanup.Invocations);
    }

    [Fact]
    public async Task CleanupFailureDoesNotMaskPipelineResult()
    {
        var catalog = new FakeCatalog()
            .Operation("op", StubOperation.AlwaysPass())
            .Cleanup("cleanup", new StubCleanup(succeeds: false));
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("op") },
            cleanup: new[] { new CleanupDeclaration("cleanup") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
        Assert.Equal(CleanupStatus.SomeFailed, report.CleanupStatus);
    }

    [Fact]
    public async Task RetriesStopAtDeclaredBound()
    {
        var op = new StubOperation(_ => OperationResult.Fail("still failing"));
        var catalog = new FakeCatalog().Operation("op", op);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("op", Retry: new RetryPolicy(3)) });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(3, op.Invocations);
    }

    [Fact]
    public async Task TimeoutIsClassifiedDistinctlyFromAssertionFailure()
    {
        var op = new DelayOperation(TimeSpan.FromMilliseconds(500));
        var catalog = new FakeCatalog().Operation("op", op);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[]
        {
            new PipelineStep("op", Retry: new RetryPolicy(1, TimeSpan.FromMilliseconds(20))),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Infrastructure, report.Classification);
    }

    [Fact]
    public async Task SameResourceKeySerializesExecution()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var gate = new object();

        var op = new InlineOperation(async (_, ct) =>
        {
            lock (gate)
            {
                concurrent++;
                maxConcurrent = Math.Max(maxConcurrent, concurrent);
            }

            await Task.Delay(50, ct);

            lock (gate)
            {
                concurrent--;
            }

            return OperationResult.Pass();
        });

        var catalog = new FakeCatalog().Operation("op", op);
        var executor = BuildExecutor(catalog);
        var resourceKey = new ResourceKey("shared-resource");
        var caseA = BuildCase(pipeline: new[] { new PipelineStep("op") }, resourceKey: resourceKey) with { CaseId = "A" };
        var caseB = BuildCase(pipeline: new[] { new PipelineStep("op") }, resourceKey: resourceKey) with { CaseId = "B" };

        await Task.WhenAll(executor.ExecuteAsync(caseA), executor.ExecuteAsync(caseB));

        Assert.Equal(1, maxConcurrent);
    }

    private sealed class InlineOperation : IOperation
    {
        private readonly Func<CaseExecutionContext, CancellationToken, Task<OperationResult>> _behavior;
        public InlineOperation(Func<CaseExecutionContext, CancellationToken, Task<OperationResult>> behavior) => _behavior = behavior;
        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            _behavior(context, cancellationToken);
    }

    [Fact]
    public async Task DistinctClassificationsForDistinctCauses()
    {
        var catalog = new FakeCatalog()
            .Prerequisite("pre", new StubPrerequisite(passed: false))
            .Operation("assertionOp", StubOperation.AlwaysFail())
            .Operation("timeoutOp", new DelayOperation(TimeSpan.FromMilliseconds(200)));
        var executor = BuildExecutor(catalog);

        var prereqReport = await executor.ExecuteAsync(BuildCase(
            prerequisites: new[] { new PrerequisiteDeclaration("pre", "QA") }));
        var assertionReport = await executor.ExecuteAsync(BuildCase(
            pipeline: new[] { new PipelineStep("assertionOp") }) with { CaseId = "assertion" });
        var timeoutReport = await executor.ExecuteAsync(BuildCase(
            pipeline: new[] { new PipelineStep("timeoutOp", Retry: new RetryPolicy(1, TimeSpan.FromMilliseconds(20))) }) with { CaseId = "timeout" });

        Assert.Equal(FailureClassification.Prerequisite, prereqReport.Classification);
        Assert.Equal(FailureClassification.Product, assertionReport.Classification);
        Assert.Equal(FailureClassification.Infrastructure, timeoutReport.Classification);
    }

    [Fact]
    public async Task ReportIsCompleteForAPassingCase()
    {
        var catalog = new FakeCatalog()
            .Operation("op", StubOperation.AlwaysPass())
            .Cleanup("cleanup", new StubCleanup());
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("op") },
            cleanup: new[] { new CleanupDeclaration("cleanup") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.Equal(testCase.CaseId, report.CaseId);
        Assert.Equal(testCase.Oracle, report.Oracle);
        Assert.Equal(testCase.Fixture.ExpectedSha256, report.FixtureSha256);
        Assert.True(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public async Task UnknownOperationThrowsBeforeExecution()
    {
        var catalog = new FakeCatalog();
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(pipeline: new[] { new PipelineStep("missing-op") });

        await Assert.ThrowsAsync<UnknownReferenceException>(() => executor.ExecuteAsync(testCase));
    }

    [Fact]
    public async Task MissingRequiredCapabilityIsDistinctFromAssertionFailure()
    {
        var op = StubOperation.AlwaysPass();
        var catalog = new FakeCatalog().Operation("op", op);
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("op") },
            requiredCapabilities: new[] { new CapabilityRequirement("browser:chromium") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.NotEqual(FailureClassification.Product, report.Classification);
        Assert.Equal(0, op.Invocations);
    }

    [Fact]
    public async Task MissingCapabilityTakesPriorityOverAnUnknownReference()
    {
        var catalog = new FakeCatalog();
        var executor = BuildExecutor(catalog);
        var testCase = BuildCase(
            pipeline: new[] { new PipelineStep("azdo.createWorkItem") },
            prerequisites: new[] { new PrerequisiteDeclaration("azdo.areaPathExists", "QA") },
            cleanup: new[] { new CleanupDeclaration("azdo.deleteWorkItem") },
            requiredCapabilities: new[] { new CapabilityRequirement("http:azure-devops") });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Infrastructure, report.Classification);
        Assert.Equal("missing-capability:http:azure-devops", report.FailureDetail);
    }
}
