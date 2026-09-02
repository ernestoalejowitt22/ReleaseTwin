using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class EvidenceIngestApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EvidenceIngestApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid ProjectId, Guid OrgId)> AuthedClientAsync(bool paid)
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        if (paid)
        {
            await provisioning.UpgradeToTeamAsync(user.OrganizationId);
        }

        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        return (client, project.Id, user.OrganizationId);
    }

    private static object ReportWithEvidence(object? evidence) => new
    {
        caseId = "CASE-1",
        oracleLocator = "t/1",
        fixtureSha256 = "abc",
        passed = true,
        cleanupStatus = "AllSucceeded",
        durationMs = 5,
        evidence,
    };

    private static object SampleEvidence() => new
    {
        caseId = "CASE-1",
        oracleLocator = "t/1",
        legs = new[] { new { leg = (string?)null, steps = new[] { new { index = 0, operationName = "http.request", outcome = "Passed", durationMs = 3L } } } },
        redactionNote = "Redacted by your CLI before upload.",
    };

    [Fact]
    public async Task MetadataOnlyPayload_IsAcceptedUnchanged()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: true);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<IRunEvidenceRepository>();
        Assert.Empty(await evidence.ListByProjectAsync(projectId));
    }

    [Fact]
    public async Task PaidTier_EvidenceStored_AndReportedAccepted()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: true);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(SampleEvidence()));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AckDto>();
        Assert.True(body!.EvidenceAccepted);

        using var scope = _factory.Services.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<IRunEvidenceRepository>();
        Assert.Single(await evidence.ListByProjectAsync(projectId));
    }

    [Fact]
    public async Task FreeTier_ReportStored_EvidenceDropped_AckSaysSo()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: false);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(SampleEvidence()));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AckDto>();
        Assert.False(body!.EvidenceAccepted);

        using var scope = _factory.Services.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<IRunEvidenceRepository>();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        Assert.Empty(await evidence.ListByProjectAsync(projectId));
        Assert.Single(await reports.ListByProjectAsync(projectId));
    }

    [Fact]
    public async Task OversizeEvidence_RejectsWholeRequestAtomically()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: true);
        var huge = new { blob = new string('x', 300 * 1024) };
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(huge));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ICaseReportRepository>();
        Assert.Empty(await reports.ListByProjectAsync(projectId));
    }

    // pr-annotation-evidence-link: the ingest response returns a reportUrl + runUrl so a PR
    // annotation can link into the dashboard.
    [Fact]
    public async Task Response_CarriesReportAndRunUrls_ForAMetadataOnlyUpload()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: true);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(null));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AckDto>();
        Assert.Contains($"/dashboard/reports/{body!.Id}/evidence?projectId={projectId}", body.ReportUrl);
        Assert.EndsWith($"/dashboard?projectId={projectId}", body.RunUrl);
    }

    [Fact]
    public async Task Response_StillCarriesUrls_WhenEvidenceIsNotAccepted()
    {
        var (client, projectId, _) = await AuthedClientAsync(paid: false);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ReportWithEvidence(SampleEvidence()));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AckDto>();
        Assert.False(body!.EvidenceAccepted);
        Assert.Contains($"/dashboard/reports/{body.Id}/evidence?projectId={projectId}", body.ReportUrl);
        Assert.Contains($"/dashboard?projectId={projectId}", body.RunUrl);
    }

    [Fact]
    public async Task Response_UrlsAreAbsolute_WhenWebBaseUrlIsConfigured()
    {
        await using var factory = new CustomWebApplicationFactory
        {
            ExtraConfiguration = new Dictionary<string, string?> { ["Web:BaseUrl"] = "https://app.example.com/" },
        };

        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.PostAsJsonAsync("/api/ingest/flag-proof-report", new
        {
            caseId = "CASE-1",
            oracleLocator = "t/1",
            buildIdentity = "build-1",
            outcome = "Passed",
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AckDto>();
        Assert.StartsWith("https://app.example.com/dashboard/reports/", body!.ReportUrl);
        Assert.Equal($"https://app.example.com/dashboard?projectId={project.Id}", body.RunUrl);
    }

    private sealed class AckDto
    {
        public Guid Id { get; set; }
        public bool EvidenceAccepted { get; set; }
        public string ReportUrl { get; set; } = "";
        public string RunUrl { get; set; } = "";
    }
}
