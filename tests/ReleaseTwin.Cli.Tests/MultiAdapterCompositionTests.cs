using System.Net;

namespace ReleaseTwin.Cli.Tests;

public class MultiAdapterCompositionTests
{
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"confirmed\"}", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-multi-adapter-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        return root;
    }

    [Fact]
    public async Task HttpAdapterInstallsWithNoAzureDevOpsConfig()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "http-only.yaml"), """
            id: HTTP-ONLY-1
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>(), // no AZDO_* vars at all
            output,
            httpAdapterHandlerForTesting: new FakeHttpHandler());

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS HTTP-ONLY-1", output.ToString());
    }

    [Fact]
    public async Task PartialAzureDevOpsConfigIsAClearStartupError()
    {
        var root = CreateWorkspace();
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?> { ["AZDO_ORG"] = "test-org" }, // only one of five
            output,
            httpAdapterHandlerForTesting: new FakeHttpHandler());

        Assert.NotEqual(0, exitCode);
        Assert.Contains("partially configured", output.ToString());
    }

    [Fact]
    public async Task CasesFromTwoDifferentAdaptersRunInTheSameInvocation()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "fixtures", "azdo.json"), "{}");
        File.WriteAllText(Path.Combine(root, "fixtures", "http.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "azdo-case.yaml"), """
            id: AZDO-CASE
            oracle:
              locator: t/1
            fixture:
              locator: azdo.json
            preconditions:
              - check: azdo.areaPathExists
                owner: QA
            pipeline:
              - operation: azdo.createWorkItem
            """);
        File.WriteAllText(Path.Combine(root, "cases", "http-case.yaml"), """
            id: HTTP-CASE
            oracle:
              locator: t/2
            fixture:
              locator: http.json
            pipeline:
              - operation: http.request
                with:
                  url: https://example.com/orders
              - operation: http.assertJsonPath
                with:
                  path: $.status
                  expected: confirmed
            """);
        var output = new StringWriter();

        var exitCode = await new CliRunner().RunAsync(
            Path.Combine(root, "cases"),
            new Dictionary<string, string?>
            {
                ["AZDO_ORG"] = "test-org",
                ["AZDO_PROJECT"] = "TeamProject",
                ["AZDO_PAT"] = "test-pat",
                ["AZDO_AREA_PATH"] = "TeamProject\\Area",
                ["AZDO_VARIABLE_GROUP_ID"] = "1",
            },
            output,
            azureDevOpsHandlerForTesting: new FakeAzureDevOpsHandler(),
            httpAdapterHandlerForTesting: new FakeHttpHandler());

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("PASS AZDO-CASE", text);
        Assert.Contains("PASS HTTP-CASE", text);
        Assert.Contains("2 passed, 0 failed", text);
    }
}
