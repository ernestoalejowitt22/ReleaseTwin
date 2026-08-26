using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class ValueCaptureTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(IReadOnlyList<PipelineStep> pipeline, string caseId = "CASE-1") => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        Array.Empty<CleanupDeclaration>());

    /// <summary>Declares captures from a fixed value, keyed by whatever names its step declared — a stand-in for an adapter operation producing a result.</summary>
    private sealed class CapturingProducer : IOperation
    {
        private readonly string _value;
        private readonly bool _succeeds;

        public CapturingProducer(string value = "captured-value", bool succeeds = true)
        {
            _value = value;
            _succeeds = succeeds;
        }

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
        {
            if (!_succeeds)
            {
                return Task.FromResult(OperationResult.Fail("failed-before-capturing"));
            }

            var produced = captures.ToDictionary(c => c.Name, _ => _value);
            return Task.FromResult(OperationResult.Pass(captures: produced));
        }
    }

    /// <summary>Records the resolved parameters it received, for asserting capture-reference substitution.</summary>
    private sealed class RecordingConsumer : IOperation
    {
        public IReadOnlyDictionary<string, object?>? Received { get; private set; }
        public int Invocations { get; private set; }

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
        {
            Invocations++;
            Received = parameters;
            return Task.FromResult(OperationResult.Pass());
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

    private static CaseExecutor BuildExecutor(FakeCatalog catalog) => new(catalog, catalog, catalog, catalog);

    [Fact]
    public async Task LaterStepReceivesTheCapturedValue()
    {
        var producer = new CapturingProducer("shhh-token");
        var consumer = new RecordingConsumer();
        var catalog = new FakeCatalog().Operation("produce", producer).Operation("consume", consumer);
        var executor = BuildExecutor(catalog);

        var testCase = BuildCase(new[]
        {
            new PipelineStep("produce", Capture: new[] { new CaptureDeclaration("token", "json:$.token") }),
            new PipelineStep("consume", With: new Dictionary<string, object?> { ["header"] = "Bearer {{token}}" }),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.True(report.Passed);
        Assert.Equal("Bearer shhh-token", consumer.Received!["header"]);
    }

    [Fact]
    public async Task ReferencingANameNoEarlierStepCapturedFailsClearly()
    {
        var consumer = new RecordingConsumer();
        var catalog = new FakeCatalog().Operation("consume", consumer);
        var executor = BuildExecutor(catalog);

        var testCase = BuildCase(new[]
        {
            new PipelineStep("consume", With: new Dictionary<string, object?> { ["header"] = "Bearer {{token}}" }),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Infrastructure, report.Classification);
        Assert.Equal("missing-capture:token", report.FailureDetail);
        Assert.Equal(0, consumer.Invocations);
    }

    [Fact]
    public async Task ReferencingACaptureFromAStepThatFailedBeforeCapturingFailsClearly()
    {
        var producer = new CapturingProducer(succeeds: false);
        var consumer = new RecordingConsumer();
        var catalog = new FakeCatalog().Operation("produce", producer).Operation("consume", consumer);
        var executor = BuildExecutor(catalog);

        var testCase = BuildCase(new[]
        {
            new PipelineStep("produce", Capture: new[] { new CaptureDeclaration("token", "json:$.token") }),
            new PipelineStep("consume", With: new Dictionary<string, object?> { ["header"] = "Bearer {{token}}" }),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal(0, consumer.Invocations);
    }

    [Fact]
    public async Task ATypoedCaptureNameIsTreatedAsUnavailable()
    {
        var producer = new CapturingProducer("shhh-token");
        var consumer = new RecordingConsumer();
        var catalog = new FakeCatalog().Operation("produce", producer).Operation("consume", consumer);
        var executor = BuildExecutor(catalog);

        var testCase = BuildCase(new[]
        {
            new PipelineStep("produce", Capture: new[] { new CaptureDeclaration("token", "json:$.token") }),
            new PipelineStep("consume", With: new Dictionary<string, object?> { ["header"] = "Bearer {{tocken}}" }),
        });

        var report = await executor.ExecuteAsync(testCase);

        Assert.False(report.Passed);
        Assert.Equal("missing-capture:tocken", report.FailureDetail);
        Assert.Equal(0, consumer.Invocations);
    }

    [Fact]
    public async Task CapturesDoNotLeakAcrossSeparateCaseRuns()
    {
        var producer = new CapturingProducer("case-a-token");
        var consumer = new RecordingConsumer();
        var catalog = new FakeCatalog().Operation("produce", producer).Operation("consume", consumer);
        var executor = BuildExecutor(catalog);

        var caseA = BuildCase(
            new[] { new PipelineStep("produce", Capture: new[] { new CaptureDeclaration("token", "json:$.token") }) },
            caseId: "A");
        var caseB = BuildCase(
            new[] { new PipelineStep("consume", With: new Dictionary<string, object?> { ["header"] = "Bearer {{token}}" }) },
            caseId: "B");

        var reportA = await executor.ExecuteAsync(caseA);
        var reportB = await executor.ExecuteAsync(caseB);

        Assert.True(reportA.Passed);
        Assert.False(reportB.Passed);
        Assert.Equal("missing-capture:token", reportB.FailureDetail);
    }
}
