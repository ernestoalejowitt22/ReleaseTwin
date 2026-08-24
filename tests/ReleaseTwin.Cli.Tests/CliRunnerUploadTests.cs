using System.Net;

namespace ReleaseTwin.Cli.Tests;

public class CliRunnerUploadTests
{
    private sealed class RecordingUploadHandler : HttpMessageHandler
    {
        public int Invocations { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.Created;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(ResponseStatus)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-upload-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    private static void WriteHttpOnlyCase(string root, string caseId)
    {
        File.WriteAllText(Path.Combine(root, "fixtures", $"{caseId}.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", $"{caseId}.yaml"), $"""
            id: {caseId}
            oracle:
              locator: t/{caseId}
            fixture:
              locator: {caseId}.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
    }

    private sealed class SucceedingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    // Scenario: Upload occurs when a token is configured
    [Fact]
    public async Task UploadOccursWhenTokenIsConfigured()
    {
        var root = CreateWorkspace();
        WriteHttpOnlyCase(root, "CASE-1");
        var uploadHandler = new RecordingUploadHandler();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "rtw_test" },
            output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: uploadHandler);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, uploadHandler.Invocations);
        Assert.Contains("case-report", uploadHandler.LastRequest!.RequestUri!.ToString());
    }

    // Scenario: No upload is attempted without a token
    [Fact]
    public async Task NoUploadIsAttemptedWithoutAToken()
    {
        var root = CreateWorkspace();
        WriteHttpOnlyCase(root, "CASE-1");
        var uploadHandler = new RecordingUploadHandler();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>(), // no RELEASETWIN_API_TOKEN
            output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: uploadHandler);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, uploadHandler.Invocations);
        Assert.DoesNotContain("upload", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // Scenario: Upload failure is a warning, not a case failure
    [Fact]
    public async Task UploadFailureIsAWarningNotACaseFailure()
    {
        var root = CreateWorkspace();
        WriteHttpOnlyCase(root, "CASE-1");
        var uploadHandler = new RecordingUploadHandler { ResponseStatus = HttpStatusCode.InternalServerError };
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "rtw_test" },
            output,
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: uploadHandler);

        // The case itself passed locally, so the exit code still reflects that, even though upload failed.
        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("PASS CASE-1", text);
        Assert.Contains("1 passed, 0 failed", text);
        Assert.Contains("WARN", text);
    }
}
