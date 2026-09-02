using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// security-hardening-pre-pilot D3: screenshot ids are constrained to 32-hex at the ingest boundary,
/// and blob storage is namespaced per project so one project's upload can never touch another's blob.
/// </summary>
public class ScreenshotIdAndBlobNamespacingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ScreenshotIdAndBlobNamespacingTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid ProjectId)> PaidClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        await provisioning.UpgradeToTeamAsync(user.OrganizationId);
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        return (client, project.Id);
    }

    private static MultipartFormDataContent UploadWithScreenshot(string screenshotId)
    {
        var report = JsonSerializer.Serialize(new
        {
            caseId = "CASE-1",
            oracleLocator = "t/1",
            fixtureSha256 = "abc",
            passed = true,
            cleanupStatus = "AllSucceeded",
            durationMs = 5,
            evidence = new
            {
                caseId = "CASE-1",
                oracleLocator = "t/1",
                legs = new[] { new { leg = (string?)null, steps = new[] { new { index = 0, operationName = "ui.screenshot", outcome = "Passed", durationMs = 3L } } } },
                redactionNote = "Redacted by your CLI before upload.",
            },
        });

        var content = new MultipartFormDataContent
        {
            { new StringContent(report, Encoding.UTF8), "report" },
        };
        var png = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(png, $"screenshot:{screenshotId}", $"{screenshotId}.png");
        return content;
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")] // uppercase
    [InlineData("short")]
    [InlineData("00000000000000000000000000000000zz")] // too long / non-hex
    [InlineData("0000000000000000000000000000000g")] // non-hex char
    public async Task MalformedScreenshotIdRejectsTheWholeUpload(string badId)
    {
        var (client, projectId) = await PaidClientAsync();

        var response = await client.PostAsync("/api/ingest/case-report", UploadWithScreenshot(badId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        var evidence = scope.ServiceProvider.GetRequiredService<IRunEvidenceRepository>();
        Assert.Empty(await reports.ListByProjectAsync(projectId));
        Assert.Empty(await evidence.ListByProjectAsync(projectId));
    }

    [Fact]
    public async Task WellFormedScreenshotIdIsAccepted()
    {
        var (client, projectId) = await PaidClientAsync();
        var id = Guid.NewGuid().ToString("N");

        var response = await client.PostAsync("/api/ingest/case-report", UploadWithScreenshot(id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var blobs = scope.ServiceProvider.GetRequiredService<IEvidenceBlobStore>();
        Assert.NotNull(await blobs.GetAsync(projectId, id));
    }

    [Fact]
    public void ScreenshotIdValidatorMatchesGuidN()
    {
        Assert.True(ScreenshotId.IsValid(Guid.NewGuid().ToString("N")));
        Assert.False(ScreenshotId.IsValid(null));
        Assert.False(ScreenshotId.IsValid(""));
        Assert.False(ScreenshotId.IsValid(Guid.NewGuid().ToString())); // has dashes
        Assert.False(ScreenshotId.IsValid("screenshots/" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task OneProjectCannotOverwriteAnotherProjectsBlob()
    {
        var store = new InMemoryEvidenceBlobStore();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var sharedId = Guid.NewGuid().ToString("N");

        await store.PutAsync(projectB, sharedId, new byte[] { 0xB });
        await store.PutAsync(projectA, sharedId, new byte[] { 0xA }); // same id, different project

        Assert.Equal(new byte[] { 0xB }, await store.GetAsync(projectB, sharedId));
        Assert.Equal(new byte[] { 0xA }, await store.GetAsync(projectA, sharedId));
    }

    [Fact]
    public async Task DeleteIsScopedToOneProjectsNamespace()
    {
        var store = new InMemoryEvidenceBlobStore();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var id = Guid.NewGuid().ToString("N");
        await store.PutAsync(projectA, id, new byte[] { 1 });
        await store.PutAsync(projectB, id, new byte[] { 2 });

        await store.DeleteAsync(projectA, id);

        Assert.Null(await store.GetAsync(projectA, id));
        Assert.NotNull(await store.GetAsync(projectB, id));
    }

    [Fact]
    public async Task LegacyFlatKeyBlobsStillResolve()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rt-blob-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id = Guid.NewGuid().ToString("N");
            // Simulate a blob written before project-namespacing: a bare <id>.png at the store root.
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, id + ".png"), new byte[] { 7 });

            var store = new FileSystemEvidenceBlobStore(dir);
            Assert.Equal(new byte[] { 7 }, await store.GetAsync(Guid.NewGuid(), id));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
