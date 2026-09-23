using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ReleaseTwin.Cli.Tests;

public class CliRunnerJourneyTests
{
    private static Dictionary<string, string?> ValidEnvironment() => new()
    {
        ["RELEASETWIN_API_TOKEN"] = "rtw_test",
    };

    private sealed class FakeJourneyHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeJourneyHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string CreateFixturesRoot()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-journey-fixtures-").FullName;
        File.WriteAllText(Path.Combine(root, "f.json"), "{}");
        return root;
    }

    // Scenario: A hosted journey runs like a local case
    [Fact]
    public async Task AFetchedJourneyRunsThroughTheSamePipelineMachinery()
    {
        var fixturesRoot = CreateFixturesRoot();
        var journeyId = Guid.NewGuid();
        var yaml = $$"""
            id: HOSTED-1
            oracle:
              locator: t/HOSTED-1
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/ok
            """;
        var handler = new FakeJourneyHandler(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
        {
            journeyId,
            version = 3,
            yamlContent = yaml,
        }));

        var env = new Dictionary<string, string?>(ValidEnvironment()) { ["RELEASETWIN_FIXTURES_ROOT"] = fixturesRoot };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 3, env, output,
            httpAdapterHandlerForTesting: new FakeJourneyHandler(HttpStatusCode.OK, "{}"),
            journeyFetchHandlerForTesting: handler);

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS HOSTED-1", output.ToString());
        Assert.Contains($"/api/cli/journeys/{journeyId}/versions/3", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer rtw_test", handler.LastRequest.Headers.Authorization!.ToString());
    }

    private sealed class RecordingStateHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"state\":\"applied\"}", Encoding.UTF8, "application/json"),
            });
        }
    }

    // journey-branching: a pinned hosted journey with a choice and a guard runs through the same
    // loader + executor as a local case — only the taken branch's requests are sent.
    [Fact]
    public async Task AFetchedJourneyWithAChoiceRunsOnlyTheTakenBranch()
    {
        var fixturesRoot = CreateFixturesRoot();
        var journeyId = Guid.NewGuid();
        var yaml = """
            id: HOSTED-BRANCH
            oracle:
              locator: t/HOSTED-BRANCH
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/state
                capture:
                  - name: promoState
                    from: json:$.state
              - kind: "choice"
                when:
                  ref: "promoState"
                  op: "=="
                  value: "applied"
                then:
                  - operation: http.request
                    with:
                      url: https://example.com/applied
                else:
                  - operation: http.request
                    with:
                      url: https://example.com/pending
              - operation: http.request
                when:
                  ref: "promoState"
                  op: "!="
                  value: "applied"
                with:
                  url: https://example.com/guarded
              - operation: http.request
                with:
                  url: https://example.com/after
            """;
        var fetch = new FakeJourneyHandler(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
        {
            journeyId,
            version = 7,
            yamlContent = yaml,
        }));
        var http = new RecordingStateHandler();
        var env = new Dictionary<string, string?>(ValidEnvironment()) { ["RELEASETWIN_FIXTURES_ROOT"] = fixturesRoot };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 7, env, output,
            httpAdapterHandlerForTesting: http,
            journeyFetchHandlerForTesting: fetch);

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS HOSTED-BRANCH", output.ToString());
        Assert.Equal(new[] { "/state", "/applied", "/after" }, http.Paths);
    }

    [Fact]
    public async Task AFetchedJourneyWithANestedChoiceIsAClearParseErrorAndRunsNothing()
    {
        var journeyId = Guid.NewGuid();
        var yaml = """
            id: HOSTED-NESTED
            oracle:
              locator: t/HOSTED-NESTED
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with: { url: "https://example.com/state" }
                capture: [{ name: s, from: "json:$.state" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - kind: choice
                    when: { ref: s, op: exists }
                    then: []
                    else: []
                else: []
            """;
        var fetch = new FakeJourneyHandler(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new { journeyId, version = 2, yamlContent = yaml }));
        var http = new RecordingStateHandler();
        var env = new Dictionary<string, string?>(ValidEnvironment()) { ["RELEASETWIN_FIXTURES_ROOT"] = CreateFixturesRoot() };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 2, env, output, httpAdapterHandlerForTesting: http, journeyFetchHandlerForTesting: fetch);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Failed to parse fetched journey {journeyId} version 2", output.ToString());
        Assert.Contains("cannot be nested", output.ToString());
        Assert.Empty(http.Paths);
    }

    // Scenario: A fetch failure is a clear error, not a silent no-op
    [Fact]
    public async Task AFailedFetchIsAClearErrorNotASilentNoOp()
    {
        var journeyId = Guid.NewGuid();
        var handler = new FakeJourneyHandler(HttpStatusCode.NotFound, "not found");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 1, ValidEnvironment(), output, journeyFetchHandlerForTesting: handler);

        Assert.Equal(1, exitCode);
        Assert.Contains("Failed to fetch journey", output.ToString());
    }

    [Fact]
    public async Task RunningAHostedJourneyWithoutAnApiTokenIsARejectedNotAttempted()
    {
        var journeyId = Guid.NewGuid();
        var handler = new FakeJourneyHandler(HttpStatusCode.OK, "{}");
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 1, new Dictionary<string, string?>(), output, journeyFetchHandlerForTesting: handler);

        Assert.Equal(1, exitCode);
        Assert.Contains("RELEASETWIN_API_TOKEN", output.ToString());
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task InvalidYamlFetchedFromTheHostReportsAClearParseError()
    {
        var journeyId = Guid.NewGuid();
        var handler = new FakeJourneyHandler(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
        {
            journeyId,
            version = 1,
            yamlContent = "id: MISSING-EVERYTHING-ELSE",
        }));
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunJourneyAsync(
            journeyId, 1, ValidEnvironment(), output, journeyFetchHandlerForTesting: handler);

        Assert.Equal(1, exitCode);
        Assert.Contains("Failed to parse fetched journey", output.ToString());
    }
}
