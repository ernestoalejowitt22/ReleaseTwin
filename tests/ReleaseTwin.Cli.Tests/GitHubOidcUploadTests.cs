using System.Net;
using System.Text.Json;
using ReleaseTwin.Cli.Upload;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// github-oidc-upload (cli-runner delta): credential resolution order, the loud OIDC failure path,
/// and the summary's `upload` block. The fake handler plays both GitHub's token endpoint and the
/// ReleaseTwin exchange + ingest endpoints, routed by URL.
/// </summary>
public class GitHubOidcUploadTests
{
    private const string ProjectId = "11111111-1111-1111-1111-111111111111";
    private const string GitHubRequestUrl = "https://run-actions-1.actions.githubusercontent.com/token?api-version=2.0";

    /// <summary>Routes GitHub's token request, the exchange, and ingest; records what it saw.</summary>
    private sealed class FakeCiHandler : HttpMessageHandler
    {
        public HttpStatusCode ExchangeStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode GitHubStatus { get; set; } = HttpStatusCode.OK;
        public List<HttpRequestMessage> Requests { get; } = new();
        public string? GitHubAudienceRequested { get; private set; }
        public string? GitHubBearer { get; private set; }
        public string? ExchangeBody { get; private set; }
        public string? IngestBearer { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var url = request.RequestUri!.ToString();

            if (url.StartsWith(GitHubRequestUrl.Split('?')[0]))
            {
                GitHubAudienceRequested = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["audience"];
                GitHubBearer = request.Headers.Authorization?.Parameter;
                return Json(GitHubStatus, "{\"value\":\"eyJ.github.jwt\"}");
            }

            if (url.EndsWith(UploadCredentialResolver.ExchangePath))
            {
                ExchangeBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json(ExchangeStatus, "{\"token\":\"rtw_exchanged\",\"expiresAt\":\"2026-09-12T02:00:00+00:00\"}");
            }

            IngestBearer = request.Headers.Authorization?.Parameter;
            return Json(HttpStatusCode.Created, "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"runUrl\":\"https://releasetwin.com/dashboard?projectId=" + ProjectId + "\"}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }

    private sealed class SucceedingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static string CreateWorkspace(string? manifest = null)
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-oidc-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "CASE-1.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "CASE-1.yaml"), """
            id: CASE-1
            oracle:
              locator: t/CASE-1
            fixture:
              locator: CASE-1.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
        if (manifest is not null)
        {
            File.WriteAllText(Path.Combine(root, "cases", "releasetwin.yml"), manifest);
        }

        return root;
    }

    private static Dictionary<string, string?> GitHubJobEnvironment(bool withOidcPair = true, string? projectId = ProjectId) => new()
    {
        ["RELEASETWIN_API_URL"] = "https://api.releasetwin.test",
        ["RELEASETWIN_PROJECT_ID"] = projectId,
        ["ACTIONS_ID_TOKEN_REQUEST_URL"] = withOidcPair ? GitHubRequestUrl : null,
        ["ACTIONS_ID_TOKEN_REQUEST_TOKEN"] = withOidcPair ? "gh-request-token" : null,
    };

    private static Func<string, string?> Env(Dictionary<string, string?> env) => key => env.TryGetValue(key, out var v) ? v : null;

    // ---- Resolver: order of precedence ----

    [Fact]
    public async Task AStoredTokenWinsOverEverythingAndTouchesNoNetwork()
    {
        var env = GitHubJobEnvironment();
        env["RELEASETWIN_API_TOKEN"] = "rtw_stored";
        var handler = new FakeCiHandler();

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(env), handler);

        Assert.Equal(UploadCredentialMode.Token, resolution.Mode);
        Assert.Equal("rtw_stored", resolution.Token);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task NothingNamedMeansNoCredentialAndNoError()
    {
        var handler = new FakeCiHandler();

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(new Dictionary<string, string?>()), handler);

        Assert.Equal(UploadCredentialMode.None, resolution.Mode);
        Assert.False(resolution.HasCredential);
        Assert.False(resolution.Failed);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AStoredTokenWithoutAUrlKeepsThePreOidcPlaceholderUrl()
    {
        var resolution = await UploadCredentialResolver.ResolveAsync(Env(new() { ["RELEASETWIN_API_TOKEN"] = "rtw_x" }), new FakeCiHandler());

        Assert.Equal(UploadCredentialResolver.LegacyTokenDefaultApiUrl, resolution.ApiUrl);
    }

    [Fact]
    public async Task ANamedProjectExchangesAGitHubTokenForACredential()
    {
        var handler = new FakeCiHandler();

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(GitHubJobEnvironment()), handler);

        Assert.Equal(UploadCredentialMode.Oidc, resolution.Mode);
        Assert.Equal("rtw_exchanged", resolution.Token);
        Assert.Equal("api.releasetwin.com", handler.GitHubAudienceRequested);
        Assert.Equal("gh-request-token", handler.GitHubBearer);
        using var body = JsonDocument.Parse(handler.ExchangeBody!);
        Assert.Equal("eyJ.github.jwt", body.RootElement.GetProperty("token").GetString());
        Assert.Equal(ProjectId, body.RootElement.GetProperty("projectId").GetString());
        Assert.Equal("https://api.releasetwin.test" + UploadCredentialResolver.ExchangePath, handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task TheOidcPathDefaultsToTheRealApiUrl()
    {
        var env = GitHubJobEnvironment();
        env.Remove("RELEASETWIN_API_URL");

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(env), new FakeCiHandler());

        Assert.Equal(UploadCredentialMode.Oidc, resolution.Mode);
        Assert.Equal(UploadCredentialResolver.DefaultApiUrl, resolution.ApiUrl);
    }

    // ---- Resolver: loud failures name the fix ----

    [Fact]
    public async Task ANamedProjectWithoutTheOidcPairNamesThePermission()
    {
        var resolution = await UploadCredentialResolver.ResolveAsync(Env(GitHubJobEnvironment(withOidcPair: false)), new FakeCiHandler());

        Assert.Equal(UploadCredentialMode.OidcExchangeFailed, resolution.Mode);
        Assert.Contains("id-token: write", resolution.FailureReason);
        Assert.Contains("RELEASETWIN_API_TOKEN", resolution.FailureReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "not bound")]
    [InlineData(HttpStatusCode.Conflict, "already exchanged")]
    [InlineData(HttpStatusCode.Unauthorized, "rejected the GitHub OIDC token")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate-limited")]
    public async Task ARefusedExchangeExplainsItself(HttpStatusCode status, string expectedFragment)
    {
        var handler = new FakeCiHandler { ExchangeStatus = status };

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(GitHubJobEnvironment()), handler);

        Assert.Equal(UploadCredentialMode.OidcExchangeFailed, resolution.Mode);
        Assert.Contains(expectedFragment, resolution.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(resolution.Token);
    }

    [Fact]
    public async Task GitHubRefusingToIssueATokenIsReported()
    {
        var handler = new FakeCiHandler { GitHubStatus = HttpStatusCode.Forbidden };

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(GitHubJobEnvironment()), handler);

        Assert.True(resolution.Failed);
        Assert.Contains("GitHub refused", resolution.FailureReason);
        Assert.Single(handler.Requests); // never reached the exchange
    }

    [Fact]
    public async Task AMalformedProjectIdIsReportedBeforeAnyRequest()
    {
        var handler = new FakeCiHandler();

        var resolution = await UploadCredentialResolver.ResolveAsync(Env(GitHubJobEnvironment(projectId: "express-demo")), handler);

        Assert.True(resolution.Failed);
        Assert.Contains("not a project id", resolution.FailureReason);
        Assert.Empty(handler.Requests);
    }

    // ---- Runner integration ----

    [Fact]
    public async Task ARunUploadsWithTheExchangedCredentialAndRecordsTheMode()
    {
        var root = CreateWorkspace();
        var summaryPath = Path.Combine(root, "summary.json");
        var env = GitHubJobEnvironment();
        env["RELEASETWIN_SUMMARY_JSON"] = summaryPath;
        var handler = new FakeCiHandler();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: handler);

        Assert.Equal(0, exitCode);
        Assert.Equal("rtw_exchanged", handler.IngestBearer);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().Contains("case-report"));
        Assert.DoesNotContain("rtw_exchanged", output.ToString()); // the credential is never printed
        using var summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal("oidc", summary.RootElement.GetProperty("upload").GetProperty("mode").GetString());
        Assert.False(summary.RootElement.GetProperty("upload").TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task ARunWithAStoredTokenRecordsTokenModeAndAnUnconfiguredRunRecordsNone()
    {
        var root = CreateWorkspace();
        var summaryPath = Path.Combine(root, "summary.json");
        var handler = new FakeCiHandler();

        await new CliRunner().RunAsync(Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "rtw_stored", ["RELEASETWIN_SUMMARY_JSON"] = summaryPath },
            new StringWriter(), httpAdapterHandlerForTesting: new SucceedingHttpHandler(), uploadHandlerForTesting: handler);
        Assert.Equal("token", JsonDocument.Parse(File.ReadAllText(summaryPath)).RootElement.GetProperty("upload").GetProperty("mode").GetString());

        await new CliRunner().RunAsync(Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_SUMMARY_JSON"] = summaryPath },
            new StringWriter(), httpAdapterHandlerForTesting: new SucceedingHttpHandler(), uploadHandlerForTesting: handler);
        Assert.Equal("none", JsonDocument.Parse(File.ReadAllText(summaryPath)).RootElement.GetProperty("upload").GetProperty("mode").GetString());
    }

    // Design D5: naming a project is declared intent, so a failed exchange is loud — non-zero exit,
    // the fix in one line, and a summary that says why nothing landed.
    [Fact]
    public async Task AFailedExchangeStopsTheRunLoudlyAndLeavesAnExplanatorySummary()
    {
        var root = CreateWorkspace();
        var summaryPath = Path.Combine(root, "summary.json");
        var env = GitHubJobEnvironment(withOidcPair: false);
        env["RELEASETWIN_SUMMARY_JSON"] = summaryPath;
        var handler = new FakeCiHandler();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"), env, output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: handler);

        Assert.Equal(1, exitCode);
        Assert.Contains("ERROR: hosted upload could not authenticate", output.ToString());
        Assert.Contains("id-token: write", output.ToString());
        Assert.DoesNotContain("PASS CASE-1", output.ToString()); // nothing ran
        using var summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal("failed", summary.RootElement.GetProperty("overall").GetString());
        Assert.Equal(0, summary.RootElement.GetProperty("totals").GetProperty("cases").GetInt32());
        Assert.Equal("oidc-exchange-failed", summary.RootElement.GetProperty("upload").GetProperty("mode").GetString());
        Assert.Contains("id-token: write", summary.RootElement.GetProperty("upload").GetProperty("reason").GetString());
    }

    // ---- Manifest `project:` ----

    [Fact]
    public async Task TheManifestCanNameTheProjectAndTheEnvironmentWinsWhenBothAreSet()
    {
        var root = CreateWorkspace(manifest: $"project: {ProjectId}\n");
        var handler = new FakeCiHandler();
        var env = GitHubJobEnvironment(projectId: null);

        var exitCode = await new CliRunner().RunAsync(Path.Combine(root, "cases"), env, new StringWriter(),
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(), uploadHandlerForTesting: handler);

        Assert.Equal(0, exitCode);
        Assert.Contains(ProjectId, handler.ExchangeBody);

        var overriding = new FakeCiHandler();
        var envWins = GitHubJobEnvironment(projectId: "33333333-3333-3333-3333-333333333333");
        await new CliRunner().RunAsync(Path.Combine(root, "cases"), envWins, new StringWriter(),
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(), uploadHandlerForTesting: overriding);
        Assert.Contains("33333333-3333-3333-3333-333333333333", overriding.ExchangeBody);
    }

    [Fact]
    public void AManifestWithoutAProjectOrWithoutAManifestYieldsNull()
    {
        Assert.Null(CaseLoading.CaseFileLoader.ReadManifestProjectId(CreateWorkspace() + "/cases"));
        Assert.Null(CaseLoading.CaseFileLoader.ReadManifestProjectId(CreateWorkspace(manifest: "flag_proof:\n  control:\n    method: PUT\n    url: https://x/{{featureKey}}\n    body: '{}'\n    known_bad_when: disabled\n") + "/cases"));
    }

    // ---- upload-junit ----

    [Fact]
    public async Task UploadJUnitUsesTheExchangedCredentialAndFailsLoudlyWithoutAnyCredential()
    {
        var root = CreateWorkspace();
        var junit = Path.Combine(root, "junit.xml");
        File.WriteAllText(junit, "<testsuite/>");
        var handler = new FakeCiHandler();
        var output = new StringWriter();

        var exitCode = await JUnitUploadCommand.RunAsync([junit], GitHubJobEnvironment(), output, handler);

        Assert.Equal(0, exitCode);
        Assert.Equal("rtw_exchanged", handler.IngestBearer);

        var none = new StringWriter();
        var noneExit = await JUnitUploadCommand.RunAsync([junit], new Dictionary<string, string?>(), none, new FakeCiHandler());
        Assert.Equal(1, noneExit);
        Assert.Contains("RELEASETWIN_API_TOKEN", none.ToString());
        Assert.Contains("RELEASETWIN_PROJECT_ID", none.ToString());

        var failed = new StringWriter();
        var failedExit = await JUnitUploadCommand.RunAsync([junit], GitHubJobEnvironment(withOidcPair: false), failed, new FakeCiHandler());
        Assert.Equal(1, failedExit);
        Assert.Contains("id-token: write", failed.ToString());
    }
}
