using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui.Tests;

/// <summary>
/// One Playwright/browser instance is shared across every test in this class. Playwright's .NET
/// driver gets flaky after many create/dispose cycles in a single process (video recording is the
/// first thing to break) — and in production the CLI only ever constructs one <see cref="UiAdapter"/>
/// per run. Each test still gets a fresh browser context via its <c>ui.closePage</c> cleanup.
/// </summary>
public sealed class UiAdapterFixture : IAsyncLifetime
{
    public UiAdapter Adapter { get; private set; } = null!;
    public StaticPageServer Server { get; } = new();

    public async Task InitializeAsync() => Adapter = await UiAdapter.CreateAsync();

    public Task DisposeAsync()
    {
        Adapter.Dispose();
        Server.Dispose();
        return Task.CompletedTask;
    }
}

public class UiAdapterTests : IClassFixture<UiAdapterFixture>
{
    private readonly UiAdapter _adapter;
    private readonly StaticPageServer _server;

    public UiAdapterTests(UiAdapterFixture fixture)
    {
        _adapter = fixture.Adapter;
        _server = fixture.Server;
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

    // ui-adapter delta: a cookie seeded by one step is visible to a later navigation — proving both
    // ui.setCookie and that all ui.* steps in a run share one browser context.
    [Fact]
    public async Task SeededCookieIsSentOnALaterNavigation()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-COOKIE-1", new[]
        {
            new PipelineStep("ui.setCookie", With: new Dictionary<string, object?>
            {
                ["name"] = "sid",
                ["value"] = "e2e-value",
                ["domain"] = "127.0.0.1",
            }),
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.waitFor", With: new Dictionary<string, object?>
            {
                ["selector"] = "text=sid=e2e-value",
                ["timeoutMs"] = 3000,
            }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.True(report.Passed, report.FailureDetail);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public async Task WithoutSetCookieTheNavigationSeesNoCookie()
    {
        var executor = BuildExecutor();

        var report = await executor.ExecuteAsync(BuildCase("UI-COOKIE-2", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.waitFor", With: new Dictionary<string, object?>
            {
                ["selector"] = "text=sid=e2e-value",
                ["timeoutMs"] = 1500,
            }),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.False(report.Passed);
    }

    [Theory]
    [InlineData("neither", null, null)]
    [InlineData("both", "http://127.0.0.1/", "127.0.0.1")]
    [InlineData("relative-url", "not-a-url", null)]
    public async Task SetCookieRejectsAMalformedScope(string _, string? url, string? domain)
    {
        var executor = BuildExecutor();

        var with = new Dictionary<string, object?> { ["name"] = "x", ["value"] = "y" };
        if (url is not null)
        {
            with["url"] = url;
        }

        if (domain is not null)
        {
            with["domain"] = domain;
        }

        var report = await executor.ExecuteAsync(BuildCase("UI-COOKIE-3", new[]
        {
            new PipelineStep("ui.setCookie", With: with),
        }, new[] { new CleanupDeclaration("ui.closePage") }));

        Assert.False(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    // evidence-capture delta: a value typed into a password field is masked in the adapter, before
    // it can reach uploaded evidence, and flagged so no allowlist entry re-exposes it.
    [Fact]
    public async Task FillIntoAPasswordFieldMasksTheValueInEvidence()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync(BuildCase("UI-PW-1", new[]
        {
            new PipelineStep("ui.navigate", With: new Dictionary<string, object?> { ["url"] = _server.Url }),
            new PipelineStep("ui.fill", With: new Dictionary<string, object?> { ["selector"] = "#secret", ["value"] = "hunter2" }),
            new PipelineStep("ui.fill", With: new Dictionary<string, object?> { ["selector"] = "#name", ["value"] = "not-secret" }),
        }, new[] { new CleanupDeclaration("ui.closePage") }), new ExecutionOptions { CaptureEvidence = true });

        Assert.True(result.Report.Passed, result.Report.FailureDetail);
        var pwStep = Assert.IsType<UiStepEvidence>(result.Evidence!.Steps[1].AdapterEvidence);
        Assert.True(pwStep.ValueIsProtected);
        Assert.Equal("«password»", pwStep.Parameters["value"]);
        Assert.DoesNotContain("hunter2", System.Text.Json.JsonSerializer.Serialize(result.Evidence));

        var plainStep = Assert.IsType<UiStepEvidence>(result.Evidence.Steps[2].AdapterEvidence);
        Assert.False(plainStep.ValueIsProtected);
        Assert.Equal("not-secret", plainStep.Parameters["value"]);
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
