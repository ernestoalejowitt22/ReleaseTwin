using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class IngestApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IngestApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(string RawToken, Guid ProjectId)> SeedTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();

        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id);
        return (raw, project.Id);
    }

    private static IngestCaseReportRequest ValidCaseReport() => new()
    {
        CaseId = "CASE-1",
        OracleLocator = "tickets/CASE-1",
        FixtureSha256 = "abc123",
        Passed = true,
        CleanupStatus = "AllSucceeded",
        DurationMs = 42,
    };

    // Scenario: Missing or invalid token is rejected
    [Fact]
    public async Task MissingTokenIsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ValidCaseReport());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTokenIsRejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "rtw_not-a-real-token");

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ValidCaseReport());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: Report is attributed to the correct project
    [Fact]
    public async Task ReportIsAttributedToTheCorrectProject()
    {
        var (raw, projectId) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ValidCaseReport());

        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HostedDbContext>();
        var stored = await db.UploadedCaseReports.SingleAsync(r => r.CaseId == "CASE-1");
        Assert.Equal(projectId, stored.ProjectId);
    }

    // Scenario: Revoked token is rejected (also covered at the service level; here through the real HTTP pipeline)
    [Fact]
    public async Task RevokedTokenIsRejectedThroughTheRealPipeline()
    {
        Guid tokenId;
        string raw;
        using (var scope = _factory.Services.CreateScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
            var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester2", null);
            var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
            var issued = await provisioning.IssueTokenAsync(project.Id);
            tokenId = issued.Token.Id;
            raw = issued.RawValue;
            await provisioning.RevokeTokenAsync(tokenId);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ValidCaseReport());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: Malformed report is rejected atomically
    [Fact]
    public async Task MalformedReportIsRejectedAtomically()
    {
        var (raw, _) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        int countBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            countBefore = await scope.ServiceProvider.GetRequiredService<HostedDbContext>().UploadedCaseReports.CountAsync();
        }

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", new { caseId = "" });

        Assert.False(response.IsSuccessStatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var countAfter = await verifyScope.ServiceProvider.GetRequiredService<HostedDbContext>().UploadedCaseReports.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    // Scenario: Payload shape excludes sensitive fields
    [Fact]
    public void ContractSchemaExcludesSensitiveFields()
    {
        var forbiddenSubstrings = new[] { "fixturecontent", "responsebody", "body", "credential", "password", "secret" };
        var fieldNames = typeof(IngestCaseReportRequest).GetProperties()
            .Concat(typeof(IngestFlagProofReportRequest).GetProperties())
            .Select(p => p.Name.ToLowerInvariant());

        foreach (var field in fieldNames)
        {
            Assert.DoesNotContain(forbiddenSubstrings, forbidden => field.Contains(forbidden));
        }
    }

    // Flag-proof report happy path
    [Fact]
    public async Task FlagProofReportIsAccepted()
    {
        var (raw, projectId) = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/flag-proof-report", new IngestFlagProofReportRequest
        {
            CaseId = "CLM-042",
            OracleLocator = "tickets/CLM-042",
            BuildIdentity = "build-123",
            Outcome = "Passed",
            KnownBadLegPassed = false,
            KnownGoodLegPassed = true,
        });

        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HostedDbContext>();
        var stored = await db.UploadedFlagProofReports.SingleAsync();
        Assert.Equal(projectId, stored.ProjectId);
        Assert.Equal("Passed", stored.Outcome);
    }
}
