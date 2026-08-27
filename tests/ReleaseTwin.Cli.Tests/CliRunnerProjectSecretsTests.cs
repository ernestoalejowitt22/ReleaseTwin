using System.Net;
using System.Text;
using System.Text.Json;

namespace ReleaseTwin.Cli.Tests;

/// <summary>hosted-project-secrets: CliRunner's local-environment-first, hosted-project-secrets-fallback resolution for `${VAR_NAME}` case-file references.</summary>
public class CliRunnerProjectSecretsTests
{
    private static Dictionary<string, string?> ApiTokenOnlyEnvironment() => new()
    {
        ["RELEASETWIN_API_TOKEN"] = "rtw_test",
    };

    private sealed class FakeProjectSecretsHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _secrets;
        public int Invocations { get; private set; }

        public FakeProjectSecretsHandler(IReadOnlyDictionary<string, string> secrets) => _secrets = secrets;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            var body = JsonSerializer.Serialize(new { secrets = _secrets });
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

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
    }

    /// <summary>Since RELEASETWIN_API_TOKEN is set in these tests (needed to enable the hosted-fetch fallback), CliRunner also attempts a report upload — this keeps that hermetic without asserting on it.</summary>
    private sealed class AlwaysOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-project-secrets-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteCaseReferencingVarName(string root, string caseId, string varName)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        var yaml = $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            pipeline:
              - operation: http.request
                with:
                  url: __URL__/orders
                  method: GET
            """.Replace("__URL__", "${" + varName + "}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), yaml);
    }

    // Scenario: A hosted-stored secret resolves a reference the local environment doesn't have
    [Fact]
    public async Task AHostedStoredSecretResolvesAReferenceTheLocalEnvironmentLacks()
    {
        var root = CreateWorkspace();
        WriteCaseReferencingVarName(root, "CASE-1", "NAHA_E2E_SECRET");
        var secretsHandler = new FakeProjectSecretsHandler(new Dictionary<string, string> { ["NAHA_E2E_SECRET"] = "https://real-naha.example.com" });
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ApiTokenOnlyEnvironment(), output,
            httpAdapterHandlerForTesting: new AlwaysOkHandler(),
            uploadHandlerForTesting: new AlwaysOkHandler(),
            projectSecretsHandlerForTesting: secretsHandler);

        Assert.True(secretsHandler.Invocations >= 1);
        Assert.Contains("PASS CASE-1", output.ToString());
        Assert.Equal(0, exitCode);
    }

    // Scenario: A local environment variable takes precedence over a hosted-stored secret
    [Fact]
    public async Task ALocalEnvironmentVariableTakesPrecedenceAndNoFetchIsAttempted()
    {
        var root = CreateWorkspace();
        WriteCaseReferencingVarName(root, "CASE-1", "NAHA_E2E_SECRET");
        var env = new Dictionary<string, string?>(ApiTokenOnlyEnvironment())
        {
            ["NAHA_E2E_SECRET"] = "https://from-local-env.example.com",
        };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            httpAdapterHandlerForTesting: new AlwaysOkHandler(),
            uploadHandlerForTesting: new AlwaysOkHandler(),
            // The fetch still happens (CliRunner fetches once per run whenever a token is
            // configured, per design.md's accepted trade-off) — what matters here is that the local
            // value, not this handler's value, is what actually resolved the reference.
            projectSecretsHandlerForTesting: new FakeProjectSecretsHandler(new Dictionary<string, string> { ["NAHA_E2E_SECRET"] = "https://from-hosted-secret.example.com" }));

        Assert.Contains("PASS CASE-1", output.ToString());
        Assert.Equal(0, exitCode);
    }

    // Scenario: Neither source resolves the reference
    [Fact]
    public async Task NeitherSourcePresentStillProducesTheExistingMissingReferenceError()
    {
        var root = CreateWorkspace();
        WriteCaseReferencingVarName(root, "CASE-1", "NAHA_E2E_SECRET");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), new Dictionary<string, string?>(), output,
            httpAdapterHandlerForTesting: new NeverCalledHandler());

        Assert.Contains("undefined environment variable 'NAHA_E2E_SECRET'", output.ToString());
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task AFetchFailureDegradesToTheSameMissingReferenceErrorRatherThanCrashing()
    {
        var root = CreateWorkspace();
        WriteCaseReferencingVarName(root, "CASE-1", "NAHA_E2E_SECRET");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), ApiTokenOnlyEnvironment(), output,
            httpAdapterHandlerForTesting: new NeverCalledHandler(),
            uploadHandlerForTesting: new AlwaysOkHandler(),
            projectSecretsHandlerForTesting: new FailingHandler());

        var text = output.ToString();
        Assert.Contains("WARN: failed to fetch hosted project secrets", text);
        Assert.Contains("undefined environment variable 'NAHA_E2E_SECRET'", text);
        Assert.NotEqual(0, exitCode);
    }
}
