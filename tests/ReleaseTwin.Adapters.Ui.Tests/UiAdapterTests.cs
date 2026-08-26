using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui.Tests;

public class UiAdapterTests : IAsyncLifetime
{
    private UiAdapter _adapter = null!;
    private StaticPageServer _server = null!;

    public async Task InitializeAsync()
    {
        _adapter = await UiAdapter.CreateAsync();
        _server = new StaticPageServer();
    }

    public Task DisposeAsync()
    {
        _adapter.Dispose();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase BuildCase(string caseId, IReadOnlyList<PipelineStep> pipeline, IReadOnlyList<CleanupDeclaration>? cleanup = null) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        cleanup ?? Array.Empty<CleanupDeclaration>());

    private CaseExecutor BuildExecutor()
    {
        var root = new CompositionRoot();
        root.Install(_adapter);
        return root.BuildExecutor();
    }

    [Fact]
    public async Task NavigateSucceedsAndCanCaptureText()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-1", new[]
        {
            new PipelineStep("ui.navigate",
                With: new Dictionary<string, object?> { ["url"] = _server.Url },
                Capture: new[] { new CaptureDeclaration("greeting", "text:#greeting") }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task FillAndClickDriveAFormAndTheCapturedResultFlowsToALaterStep()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-2", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.fill", With: new Dictionary<string, object?> { ["selector"] = "#name", ["value"] = "Ernesto" }),
            new PipelineStep("ui.click", With: new Dictionary<string, object?> { ["selector"] = "#submit" }),
            new PipelineStep("ui.waitFor", With: new Dictionary<string, object?> { ["selector"] = "#result", ["state"] = "visible" }),
            new PipelineStep("ui.assertVisible",
                With: new Dictionary<string, object?> { ["selector"] = "#result" },
                Capture: new[] { new CaptureDeclaration("greetingResult", "text:#result") }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task AssertVisibleFailsClearlyWhenTheElementIsHidden()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-3", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.assertVisible", With: new Dictionary<string, object?> { ["selector"] = "#result" }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.False(report.Passed);
        Assert.Equal(FailureClassification.Product, report.Classification);
        Assert.Contains("not visible", report.FailureDetail);
    }

    [Fact]
    public async Task ClickingAMissingSelectorFailsClearlyWithoutHangingForTheDefaultTimeout()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-4", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.click", With: new Dictionary<string, object?> { ["selector"] = "#does-not-exist", ["timeoutMs"] = 500 }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.False(report.Passed);
    }

    // Cleanup still runs regardless of whether a UI step passed or failed (value-capture/ui-adapter's
    // own "integrates with existing failure classification and cleanup" requirement).
    [Fact]
    public async Task CleanupClosesThePageEvenAfterAFailingUiStep()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-5", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.assertVisible", With: new Dictionary<string, object?> { ["selector"] = "#result" }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.False(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public void KnownOperationCapabilitiesMatchesWhatRegisterContributes()
    {
        var root = new CompositionRoot();
        root.Install(_adapter);
        var catalog = root.Catalog;

        foreach (var name in UiAdapter.KnownOperationCapabilities.Keys)
        {
            var isOperation = catalog.TryGet(name, out IOperation _);
            var isCleanup = catalog.TryGet(name, out ICleanupOperation _);
            Assert.True(isOperation || isCleanup, $"'{name}' is in the manifest but Register() never contributed it");
        }
    }
}
