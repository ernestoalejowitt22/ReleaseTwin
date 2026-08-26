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
