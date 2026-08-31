using System.Net;

namespace ReleaseTwin.Cli.Tests;

/// <summary>release-readiness-rollup: a labelled case's upload payload carries `release`.</summary>
public class CliRunnerReleaseUploadTests
{
    private sealed class BodyCapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SucceedingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static string WriteCase(string? release)
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-release-upload-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "CASE-1.json"), "{}");
        var releaseLine = release is null ? "" : $"release: \"{release}\"\n";
        File.WriteAllText(Path.Combine(root, "cases", "CASE-1.yaml"), $"""
            id: CASE-1
            {releaseLine}oracle:
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
        return Path.Combine(root, "cases");
    }

    [Fact]
    public async Task LabelledCaseUploadCarriesReleaseInThePayload()
    {
        var upload = new BodyCapturingHandler();

        var exitCode = await new CliRunner().RunAsync(
            WriteCase("4.2"),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "rtw_test" },
            new StringWriter(),
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: upload);

        Assert.Equal(0, exitCode);
        Assert.NotNull(upload.LastBody);
        Assert.Contains("\"release\":\"4.2\"", upload.LastBody);
    }

    [Fact]
    public async Task UnlabelledCaseSendsANullRelease()
    {
        var upload = new BodyCapturingHandler();

        await new CliRunner().RunAsync(
            WriteCase(null),
            new Dictionary<string, string?> { ["RELEASETWIN_API_TOKEN"] = "rtw_test" },
            new StringWriter(),
            httpAdapterHandlerForTesting: new SucceedingHttpHandler(),
            uploadHandlerForTesting: upload);

        Assert.NotNull(upload.LastBody);
        Assert.Contains("\"release\":null", upload.LastBody);
    }
}
