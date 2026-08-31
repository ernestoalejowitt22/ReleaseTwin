using ReleaseTwin.Cli.CaseLoading;

namespace ReleaseTwin.Cli.Tests;

/// <summary>release-readiness-rollup: the optional case-file `release` label — present, absent, non-string rejected.</summary>
public class CaseFileLoaderReleaseTests
{
    private static string CreateTempWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-release-tests-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        return root;
    }

    private static void WriteCase(string root, string body) =>
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), body);

    [Fact]
    public void ReleaseLabelIsParsedWhenPresent()
    {
        var root = CreateTempWorkspace();
        WriteCase(root, """
            id: MY-1
            release: "4.2"
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline: []
            """);

        var testCase = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures")).LoadAll().Single().Case;

        Assert.Equal("4.2", testCase.Release);
    }

    [Fact]
    public void NoReleaseLabelParsesToNull()
    {
        var root = CreateTempWorkspace();
        WriteCase(root, """
            id: MY-1
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline: []
            """);

        var testCase = new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures")).LoadAll().Single().Case;

        Assert.Null(testCase.Release);
    }

    [Fact]
    public void NonStringReleaseIsRejectedWithAClearMessage()
    {
        var root = CreateTempWorkspace();
        WriteCase(root, """
            id: MY-1
            release:
              - 4.2
              - 4.3
            oracle:
              locator: t/1
            fixture:
              locator: f.json
            pipeline: []
            """);

        var ex = Assert.Throws<CaseFileException>(() =>
            new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures")).LoadAll());

        Assert.Contains("release", ex.Message);
        Assert.Contains("short string", ex.Message);
    }
}
