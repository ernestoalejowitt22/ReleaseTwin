using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>release-readiness-rollup: ingest carries an optional `release` label through to storage; absence is unchanged.</summary>
public class IngestReleaseLabelTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IngestReleaseLabelTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(string Raw, Guid ProjectId)> SeedTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        return (raw, project.Id);
    }

    [Fact]
    public async Task ACaseReportWithAReleaseLabelIsStoredWithIt()
    {
        var (raw, projectId) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", new
        {
            caseId = "CASE-1",
            oracleLocator = "t/CASE-1",
            fixtureSha256 = "abc",
            passed = true,
            cleanupStatus = "AllSucceeded",
            durationMs = 1,
            release = "4.2",
        });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        var stored = (await reports.ListByProjectAsync(projectId)).Single(r => r.CaseId == "CASE-1");
        Assert.Equal("4.2", stored.Release);
    }

    [Fact]
    public async Task AFlagProofReportRoundTripsItsReleaseThroughStorage()
    {
        var (raw, projectId) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/flag-proof-report", new
        {
            caseId = "CASE-1",
            oracleLocator = "t/CASE-1",
            buildIdentity = "build-1",
            outcome = "Passed",
            release = "  4.3  ",
        });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IFlagProofReportRepository>();
        var stored = (await reports.ListByProjectAsync(projectId)).Single(r => r.CaseId == "CASE-1");
        Assert.Equal("4.3", stored.Release); // trimmed
    }

    [Fact]
    public async Task AReportWithNoReleaseIsStoredWithNullRelease()
    {
        var (raw, projectId) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", new
        {
            caseId = "CASE-2",
            oracleLocator = "t/CASE-2",
            fixtureSha256 = "abc",
            passed = true,
            cleanupStatus = "AllSucceeded",
            durationMs = 1,
        });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        var stored = (await reports.ListByProjectAsync(projectId)).Single(r => r.CaseId == "CASE-2");
        Assert.Null(stored.Release);
    }

    [Fact]
    public async Task AnOverlongReleaseLabelIsRejected()
    {
        var (raw, _) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", new
        {
            caseId = "CASE-3",
            oracleLocator = "t/CASE-3",
            fixtureSha256 = "abc",
            passed = true,
            cleanupStatus = "AllSucceeded",
            durationMs = 1,
            release = new string('x', 201),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
