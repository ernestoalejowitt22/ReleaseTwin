using ReleaseTwin.Cli.CaseLoading;

namespace ReleaseTwin.Cli.Tests;

/// <summary>hosted-project-secrets: CaseFileLoader's injectable environment-resolution seam (task 4.2) — the supplied lookup drives ${VAR_NAME} resolution when given; omitting it preserves today's exact live-environment behavior.</summary>
public class CaseFileLoaderEnvironmentSeamTests
{
    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-env-seam-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), """
            id: CASE-1
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline:
              - operation: http.request
                with:
                  url: ${SOME_NAME}/orders
            """);
        return root;
    }

    [Fact]
    public void ASuppliedResolverDrivesInterpolation()
    {
        var root = CreateWorkspace();

        var loader = new CaseFileLoader(
            Path.Combine(root, "cases"),
            Path.Combine(root, "fixtures"),
            resolveEnvironmentVariable: name => name == "SOME_NAME" ? "https://from-the-seam.example.com" : null);
        var testCase = loader.LoadAll().Single().Case;

        Assert.Equal("https://from-the-seam.example.com/orders", testCase.Pipeline[0].Parameters["url"]);
    }

    [Fact]
    public void OmittingTheResolverPreservesLiveEnvironmentBehavior()
    {
        var root = CreateWorkspace();
        Environment.SetEnvironmentVariable("SOME_NAME", "https://from-the-real-environment.example.com");
        try
        {
            var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"));
            var testCase = loader.LoadAll().Single().Case;

            Assert.Equal("https://from-the-real-environment.example.com/orders", testCase.Pipeline[0].Parameters["url"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOME_NAME", null);
        }
    }

    [Fact]
    public void ASuppliedResolverThatReturnsNullStillProducesTheClearMissingReferenceError()
    {
        var root = CreateWorkspace();

        var loader = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures"), resolveEnvironmentVariable: _ => null);

        var ex = Assert.Throws<CaseFileException>(() => loader.LoadAll());
        Assert.Contains("SOME_NAME", ex.Message);
    }
}
