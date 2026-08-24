using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Core;

namespace ReleaseTwin.Core.Tests;

public class OperationParametersTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(PipelineStep step) => new(
        "CASE-1",
        new OracleReference("tickets/CASE-1"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        new[] { step },
        Array.Empty<CleanupDeclaration>());

    private sealed class CapturingOperation : IOperation
    {
        public IReadOnlyDictionary<string, object?>? Captured { get; private set; }

        public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            Captured = parameters;
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

    [Fact]
    public async Task OperationReceivesItsStepsDeclaredParameters()
    {
        var op = new CapturingOperation();
        var catalog = new FakeCatalog().Operation("op", op);
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);
        var parameters = new Dictionary<string, object?> { ["url"] = "https://example.com", ["method"] = "GET" };

        await executor.ExecuteAsync(BuildCase(new PipelineStep("op", With: parameters)));

        Assert.NotNull(op.Captured);
        Assert.Equal("https://example.com", op.Captured!["url"]);
        Assert.Equal("GET", op.Captured!["method"]);
    }

    [Fact]
    public async Task StepWithNoParametersStillExecutes()
    {
        var op = new CapturingOperation();
        var catalog = new FakeCatalog().Operation("op", op);
        var executor = new CaseExecutor(catalog, catalog, catalog, catalog);

        var report = await executor.ExecuteAsync(BuildCase(new PipelineStep("op")));

        Assert.True(report.Passed);
        Assert.NotNull(op.Captured);
        Assert.Empty(op.Captured!);
    }
}
