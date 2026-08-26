using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.LaunchDarkly.Tests;

public class LaunchDarklyAdapterTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static LaunchDarklyOptions TestOptions() =>
        new(ApiToken: Environment.GetEnvironmentVariable("TEST_LD_TOKEN") ?? "test-token-from-env", ProjectKey: "test-project", EnvironmentKey: "production");

    [Fact]
    public void AdapterIsConstructedWithExternallySuppliedCredential()
    {
        var token = Environment.GetEnvironmentVariable("TEST_LD_TOKEN") ?? Guid.NewGuid().ToString();
        var options = new LaunchDarklyOptions(token, "test-project", "production");

        using var adapter = new LaunchDarklyAdapter(options, handler: new FakeLaunchDarklyHandler());

        Assert.Equal("launchdarkly", adapter.Name);
    }

    [Fact]
    public void AdapterSourceContainsNoCredentialLiteral()
    {
        var srcDir = FindSourceDirectory();
        var suspiciousPatterns = new[] { "apitoken = \"", "token = \"", "apikey = \"" };

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
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ReleaseTwin.Adapters.LaunchDarkly")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Could not locate src/ReleaseTwin.Adapters.LaunchDarkly from test output directory.")
            : Path.Combine(dir.FullName, "src", "ReleaseTwin.Adapters.LaunchDarkly");
    }

    private static TestCase BuildCase(string caseId, IReadOnlyList<PipelineStep> pipeline) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        Array.Empty<PrerequisiteDeclaration>(),
        pipeline,
        Array.Empty<CleanupDeclaration>());

    private static CaseExecutor BuildExecutor(LaunchDarklyAdapter adapter)
    {
        var root = new CompositionRoot();
        root.Install(adapter);
        return root.BuildExecutor();
    }

    [Fact]
    public async Task FlagProofRunsEndToEndAgainstALaunchDarklyFlag()
    {
        var handler = new FakeLaunchDarklyHandler();
        using var adapter = new LaunchDarklyAdapter(TestOptions(), flagKey: "policy-api-flag", handler: handler);
        var executor = BuildExecutor(adapter);
        var runner = new FlagProofRunner(executor, new AlwaysAvailableCapabilityCatalog(), adapter.FeatureStateController);

        var testCase = BuildCase("LD-FLAG", new[] { new PipelineStep("ld.readFeatureFlag") });

        var result = await runner.RunAsync(testCase, "policy-api-flag", buildIdentity: "build-42");

        Assert.Equal(FlagProofOutcome.Passed, result.Outcome);
    }

    [Fact]
    public async Task ReadingAFlagThatWasNeverSetFails()
    {
        var handler = new FakeLaunchDarklyHandler();
        using var adapter = new LaunchDarklyAdapter(TestOptions(), flagKey: "never-set-flag", handler: handler);
        var executor = BuildExecutor(adapter);

        var report = await executor.ExecuteAsync(BuildCase("LD-MISSING", new[] { new PipelineStep("ld.readFeatureFlag") }));

        Assert.False(report.Passed);
    }

    [Fact]
    public void KnownOperationCapabilitiesMatchesWhatRegisterContributes()
    {
        using var adapter = new LaunchDarklyAdapter(TestOptions(), handler: new FakeLaunchDarklyHandler());
        var root = new CompositionRoot();
        root.Install(adapter);
        var catalog = root.Catalog;

        foreach (var name in LaunchDarklyAdapter.KnownOperationCapabilities.Keys)
        {
            Assert.True(catalog.TryGet(name, out IOperation _), $"'{name}' is in the manifest but Register() never contributed it");
        }
    }

    private sealed class AlwaysAvailableCapabilityCatalog : ICapabilityCatalog
    {
        public bool IsAvailable(string capabilityName) => capabilityName == "flag-control:runtime";
    }
}
