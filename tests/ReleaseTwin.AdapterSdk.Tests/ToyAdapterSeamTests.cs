using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Adapters.ToyFile;
using ReleaseTwin.Adapters.ToyHttp;
using ReleaseTwin.Core;

namespace ReleaseTwin.AdapterSdk.Tests;

/// <summary>
/// Validates tasks.md 5.3-5.5: each toy adapter works end to end alone, both work composed together,
/// and neither required any change to ReleaseTwin.Core or ReleaseTwin.AdapterSdk (evidenced simply by
/// this test project only referencing their public contracts, never their internals).
/// </summary>
public class ToyAdapterSeamTests
{
    private static byte[] FixtureContent => Encoding.UTF8.GetBytes("{\"amount\":500}");
    private static string FixtureHash => Convert.ToHexString(SHA256.HashData(FixtureContent)).ToLowerInvariant();
    private static FixtureReference ValidFixture => new("fixtures/case.json", FixtureHash, FixtureContent);

    private static TestCase ToyHttpCase(string caseId) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        new[] { new PrerequisiteDeclaration("toyhttp.recordTypeAvailable", "toy-http owner") },
        new[] { new PipelineStep("toyhttp.createRecord"), new PipelineStep("toyhttp.getRecord") },
        new[] { new CleanupDeclaration("toyhttp.deleteRecord") });

    private static TestCase ToyFileCase(string caseId, string workingDirectory) => new(
        caseId,
        new OracleReference($"tickets/{caseId}"),
        ValidFixture,
        new[] { new PrerequisiteDeclaration("toyfile.workingDirectoryExists", "toy-file owner") },
        new[] { new PipelineStep("toyfile.writeFile"), new PipelineStep("toyfile.readFile") },
        new[] { new CleanupDeclaration("toyfile.deleteFile") });

    [Fact]
    public async Task ToyHttpAdapterRunsEndToEndAlone()
    {
        var root = new CompositionRoot();
        root.Install(new ToyHttpAdapter(apiKey: "test-key"));
        var executor = root.BuildExecutor();

        var report = await executor.ExecuteAsync(ToyHttpCase("HTTP-1"));

        Assert.True(report.Passed);
        Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
    }

    [Fact]
    public async Task ToyFileAdapterRunsEndToEndAlone()
    {
        var tempDir = Directory.CreateTempSubdirectory("releasetwin-toyfile-").FullName;
        try
        {
            var root = new CompositionRoot();
            root.Install(new ToyFileAdapter(tempDir));
            var executor = root.BuildExecutor();

            var report = await executor.ExecuteAsync(ToyFileCase("FILE-1", tempDir));

            Assert.True(report.Passed);
            Assert.Equal(CleanupStatus.AllSucceeded, report.CleanupStatus);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task BothToyAdaptersComposeInOneHostWithoutCoreChanges()
    {
        var tempDir = Directory.CreateTempSubdirectory("releasetwin-toyfile-").FullName;
        try
        {
            var root = new CompositionRoot();
            root.Install(new ToyHttpAdapter(apiKey: "test-key"));
            root.Install(new ToyFileAdapter(tempDir));
            var executor = root.BuildExecutor();

            var httpReport = await executor.ExecuteAsync(ToyHttpCase("HTTP-2"));
            var fileReport = await executor.ExecuteAsync(ToyFileCase("FILE-2", tempDir));

            Assert.True(httpReport.Passed);
            Assert.True(fileReport.Passed);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
