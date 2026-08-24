using ReleaseTwin.Cli.CaseLoading;

namespace ReleaseTwin.Cli.Tests;

public class CaseFileLoaderParametersTests
{
    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-params-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        return root;
    }

    [Fact]
    public void StepParametersLoadIntoThePipelineStep()
    {
        var root = CreateWorkspace();
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CASE-1
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  method: POST
                  url: https://example.com/orders
                  headers:
                    Content-Type: application/json
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
        var testCase = loader.LoadAll().Single().Case;

        var parameters = testCase.Pipeline[0].Parameters;
        Assert.Equal("POST", parameters["method"]);
        Assert.Equal("https://example.com/orders", parameters["url"]);
        var headers = Assert.IsType<Dictionary<string, object?>>(parameters["headers"]);
        Assert.Equal("application/json", headers["Content-Type"]);
    }

    [Fact]
    public void EnvironmentVariableReferenceResolvesToItsValue()
    {
        var root = CreateWorkspace();
        Environment.SetEnvironmentVariable("RELEASETWIN_TEST_URL", "https://real-api.example.com");
        try
        {
            File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
                id: CASE-1
                oracle:
                  locator: t/1
                fixture:
                  locator: f.json
                pipeline:
                  - operation: http.request
                    with:
                      url: ${RELEASETWIN_TEST_URL}/orders
                """);

            var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
            var testCase = loader.LoadAll().Single().Case;

            Assert.Equal("https://real-api.example.com/orders", testCase.Pipeline[0].Parameters["url"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RELEASETWIN_TEST_URL", null);
        }
    }

    [Fact]
    public void MissingEnvironmentVariableIsAClearLoadTimeError()
    {
        var root = CreateWorkspace();
        Environment.SetEnvironmentVariable("RELEASETWIN_TEST_MISSING", null);
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CASE-1
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  url: ${RELEASETWIN_TEST_MISSING}/orders
            """);

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("RELEASETWIN_TEST_MISSING", ex.Message);
        Assert.Contains("case1.yaml", ex.Message);
    }
}
