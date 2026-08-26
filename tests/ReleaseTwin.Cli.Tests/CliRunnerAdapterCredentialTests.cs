using System.Net;
using System.Text;
using System.Text.Json;

namespace ReleaseTwin.Cli.Tests;

/// <summary>hosted-adapter-credentials: CliRunner's env-vars-first, hosted-fetch-fallback resolution for credentialed adapters.</summary>
public class CliRunnerAdapterCredentialTests
{
    private static Dictionary<string, string?> ApiTokenOnlyEnvironment() => new()
    {
        ["RELEASETWIN_API_TOKEN"] = "rtw_test",
    };

    private sealed class FakeAdapterCredentialsHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, object> _fields;
        private readonly string _configuredAdapter;
        public int Invocations { get; private set; }

        public FakeAdapterCredentialsHandler(string configuredAdapter, IReadOnlyDictionary<string, object> fields)
        {
            _configuredAdapter = configuredAdapter;
            _fields = fields;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            var path = request.RequestUri!.AbsolutePath;
            var adapter = path.Split('/')[^1];
            if (adapter != _configuredAdapter)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var body = JsonSerializer.Serialize(new { adapter, fields = _fields });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no HTTP request expected in this test");
    }

    /// <summary>404s any adapter fetch except one, which it throws on — lets a test assert "this adapter's fetch is never attempted" while tolerating the *other* credentialed adapter's own independent (and always-attempted, since its env vars are never set in these tests) fallback fetch.</summary>
    private sealed class ForbidsAdapterFetchHandler : HttpMessageHandler
    {
        private readonly string _forbiddenAdapter;
        public ForbidsAdapterFetchHandler(string forbiddenAdapter) => _forbiddenAdapter = forbiddenAdapter;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var adapter = request.RequestUri!.AbsolutePath.Split('/')[^1];
            if (adapter == _forbiddenAdapter)
            {
                throw new InvalidOperationException($"no fetch for '{_forbiddenAdapter}' expected in this test");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>Since RELEASETWIN_API_TOKEN is set in these tests (needed to enable the hosted-fetch fallback), CliRunner also attempts a report upload — this keeps that hermetic without asserting on it.</summary>
    private sealed class AlwaysOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-adapter-cred-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteLaunchDarklyFlagProofCase(string root, string caseId)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            pipeline:
              - operation: ld.readFeatureFlag
            flag_proof:
              feature_key: release-proof-feature
              build_identity: build-123
            """);
    }

    // Scenario: A hosted-fetched credential is used when the environment has none
    [Fact]
    public async Task FullyAbsentEnvironmentFallsBackToTheHostedFetch()
    {
        var root = CreateWorkspace();
        WriteLaunchDarklyFlagProofCase(root, "LD-1");
        var credentialsHandler = new FakeAdapterCredentialsHandler("launchdarkly", new Dictionary<string, object>
        {
            ["apiToken"] = "api-abc",
            ["projectKey"] = "proj",
            ["environmentKey"] = "production",
        });
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ApiTokenOnlyEnvironment(), output,
            uploadHandlerForTesting: new AlwaysOkHandler(),
            launchDarklyHandlerForTesting: new FakeLaunchDarklyHandler(),
            adapterCredentialsHandlerForTesting: credentialsHandler);

        var text = output.ToString();
        Assert.True(credentialsHandler.Invocations >= 1);
        Assert.Contains("FLAGPROOF LD-1 (Passed)", text);
        Assert.Equal(0, exitCode);
    }

    // Scenario: Neither source configures the adapter
    [Fact]
    public async Task NeitherEnvironmentNorApiTokenLeavesTheAdapterUninstalledWithoutError()
    {
        var root = CreateWorkspace();
        WriteLaunchDarklyFlagProofCase(root, "LD-1");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), new Dictionary<string, string?>(), output,
            httpAdapterHandlerForTesting: new NeverCalledHandler());

        var text = output.ToString();
        Assert.Contains("FLAGPROOF LD-1 (Ineligible)", text);
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task APIProjectWithNoStoredCredentialLeavesTheAdapterUninstalledWithoutError()
    {
        var root = CreateWorkspace();
        WriteLaunchDarklyFlagProofCase(root, "LD-1");
        var output = new StringWriter();
        // "none" never matches a real adapter name, so every fetch (azure-devops and launchdarkly,
        // both attempted since neither has environment variables set) gets a 404 "not configured".
        var credentialsHandler = new FakeAdapterCredentialsHandler("none", new Dictionary<string, object>());

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ApiTokenOnlyEnvironment(), output,
            uploadHandlerForTesting: new AlwaysOkHandler(),
            adapterCredentialsHandlerForTesting: credentialsHandler);

        var text = output.ToString();
        Assert.Contains("FLAGPROOF LD-1 (Ineligible)", text);
        Assert.NotEqual(0, exitCode);
    }

    // Scenario: Full environment configuration is used without a hosted fetch
    [Fact]
    public async Task FullEnvironmentConfigurationNeverAttemptsAHostedFetch()
    {
        var root = CreateWorkspace();
        WriteLaunchDarklyFlagProofCase(root, "LD-1");
        var env = new Dictionary<string, string?>(ApiTokenOnlyEnvironment())
        {
            ["LAUNCHDARKLY_API_TOKEN"] = "test-token",
            ["LAUNCHDARKLY_PROJECT_KEY"] = "test-project",
            ["LAUNCHDARKLY_ENVIRONMENT_KEY"] = "production",
        };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            uploadHandlerForTesting: new AlwaysOkHandler(),
            launchDarklyHandlerForTesting: new FakeLaunchDarklyHandler(),
            adapterCredentialsHandlerForTesting: new ForbidsAdapterFetchHandler("launchdarkly"));

        var text = output.ToString();
        Assert.Contains("FLAGPROOF LD-1 (Passed)", text);
        Assert.Equal(0, exitCode);
    }

    // Scenario: Partial environment configuration is a clear startup error
    [Fact]
    public async Task PartialEnvironmentConfigurationIsAnErrorEvenWithAHostedCredentialAvailable()
    {
        var root = CreateWorkspace();
        WriteLaunchDarklyFlagProofCase(root, "LD-1");
        var env = new Dictionary<string, string?>(ApiTokenOnlyEnvironment())
        {
            ["LAUNCHDARKLY_API_TOKEN"] = "test-token",
            // LAUNCHDARKLY_PROJECT_KEY and LAUNCHDARKLY_ENVIRONMENT_KEY deliberately left unset.
        };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            adapterCredentialsHandlerForTesting: new ForbidsAdapterFetchHandler("launchdarkly"));

        Assert.Contains("LaunchDarkly is partially configured", output.ToString());
        Assert.NotEqual(0, exitCode);
    }
}
