using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Flags;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class EvidenceSharingViewShapeTests
{
    // evidence-sharing (design D7): the SharedEvidenceView is the security boundary. If a field is
    // added that could identify or link to the org / project / other runs, this fails.
    private static readonly HashSet<string> Allowed =
    [
        "CaseId", "ReportKind", "Result", "Classification", "FixtureSha256",
        "HasEvidenceDocument", "EvidenceUploadedAt", "Document", "ScreenshotIds",
    ];

    [Fact]
    public void SharedEvidenceViewExposesOnlyWhitelistedFields()
    {
        var props = typeof(SharedEvidenceView).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .ToList();

        Assert.All(props, p => Assert.Contains(p.Name, Allowed));

        foreach (var p in props)
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            Assert.NotEqual(typeof(Guid), t);
            var name = p.Name.ToLowerInvariant();
            Assert.DoesNotContain("org", name);
            Assert.DoesNotContain("project", name);
            Assert.DoesNotContain("tenant", name);
            Assert.DoesNotContain("url", name);
        }
    }
}

public class EvidenceSharingServiceTests
{
    private sealed class Harness
    {
        public InMemoryHostedTable Table { get; } = new();
        public ShareLinkRepository Links { get; }
        public CaseReportRepository CaseReports { get; }
        public RunEvidenceRepository Evidence { get; }
        public OrganizationRepository Orgs { get; }
        public EvidenceSharingService Service { get; }

        public Harness(bool flagOn = true)
        {
            Links = new ShareLinkRepository(Table);
            CaseReports = new CaseReportRepository(Table);
            var flagProofs = new FlagProofReportRepository(Table);
            Evidence = new RunEvidenceRepository(Table);
            Orgs = new OrganizationRepository(Table);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:evidence-sharing"] = flagOn ? "true" : "false",
            }).Build();
            var flags = new FlagService(new StaticFlagProvider(FlagRegistry.Load(), config), FlagRegistry.Load(),
                new StubContextFactory(), NullLogger<FlagService>.Instance);
            Service = new EvidenceSharingService(Links, CaseReports, flagProofs, Evidence, Orgs,
                TestEntitlements.Service, flags, new TokenService());
        }

        private sealed class StubContextFactory : IFlagContextFactory
        {
            public FlagContext Current(Organization? organization = null, Guid? projectId = null) =>
                new("k", "team", "hosted", "test");
        }

        public async Task<(Guid OrgId, Guid ProjectId, Guid ReportId)> SeedCaseAsync(bool passed, PlanTier tier = PlanTier.Team, bool withEvidence = true)
        {
            var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier };
            await Table.PutItemAsync(OrganizationRepository.ToItem(org));
            var projectId = Guid.NewGuid();
            var report = new UploadedCaseReport
            {
                Id = Guid.NewGuid(), ProjectId = projectId, CaseId = "CLM-7", OracleLocator = "t/CLM-7",
                FixtureSha256 = "deadbeef", Passed = passed, Classification = passed ? null : "Infrastructure",
                CleanupStatus = "AllSucceeded", DurationMs = 5, UploadedAt = DateTimeOffset.UtcNow,
            };
            await CaseReports.AddAsync(report);
            if (withEvidence)
            {
                await Evidence.AddAsync(new UploadedRunEvidence
                {
                    Id = Guid.NewGuid(), ProjectId = projectId, ReportId = report.Id, ReportKind = "case",
                    DocumentJson = """{"steps":[{"name":"POST /orders","status":"failed"}]}""",
                    ScreenshotIds = ["shot-1"], UploadedAt = DateTimeOffset.UtcNow,
                });
            }
            return (org.Id, projectId, report.Id);
        }
    }

    [Fact]
    public async Task CreateThenResolveReturnsTheRedactedEvidence()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);

        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());
        var view = await h.Service.ResolveAsync(token);

        Assert.Equal("CLM-7", view.CaseId);
        Assert.Equal("failed", view.Result);
        Assert.Equal("Infrastructure", view.Classification);
        Assert.True(view.HasEvidenceDocument);
        Assert.Contains("shot-1", view.ScreenshotIds);
        Assert.Equal("failed", view.Document!.Value.GetProperty("steps")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ResolveOfAReportWithNoUploadedEvidenceIsMetadataOnly()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false, withEvidence: false);

        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());
        var view = await h.Service.ResolveAsync(token);

        Assert.False(view.HasEvidenceDocument);
        Assert.Null(view.Document);
        Assert.Empty(view.ScreenshotIds);
        Assert.Equal("failed", view.Result);
    }

    [Fact]
    public async Task RevokedLinkStopsResolving()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (link, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        await h.Service.RevokeAsync(reportId, link.Id);

        await Assert.ThrowsAsync<ShareLinkUnavailableException>(() => h.Service.ResolveAsync(token));
    }

    [Fact]
    public async Task ExpiredLinkStopsResolving()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (link, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        link.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await h.Links.PutAsync(link);

        await Assert.ThrowsAsync<ShareLinkUnavailableException>(() => h.Service.ResolveAsync(token));
    }

    [Fact]
    public async Task FlagOffMakesEveryLinkUnavailable()
    {
        var h = new Harness(flagOn: false);
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        await Assert.ThrowsAsync<ShareLinkUnavailableException>(() => h.Service.ResolveAsync(token));
    }

    [Fact]
    public async Task LosingTheEntitlementReturnsRevokedNotDeleted()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        // Org downgraded to Free.
        await h.Orgs.SetPlanTierAsync(orgId, PlanTier.Free);

        await Assert.ThrowsAsync<ShareEntitlementRevokedException>(() => h.Service.ResolveAsync(token));

        // Restored — the link works again (it was never deleted).
        await h.Orgs.SetPlanTierAsync(orgId, PlanTier.Team);
        var view = await h.Service.ResolveAsync(token);
        Assert.Equal("CLM-7", view.CaseId);
    }

    [Fact]
    public async Task TokenForOneReportCannotFetchAnother()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        var parts = token.Split('.');
        var tamperedToken = $"{Guid.NewGuid()}.{parts[1]}";

        await Assert.ThrowsAsync<ShareLinkUnavailableException>(() => h.Service.ResolveAsync(tamperedToken));
    }

    [Fact]
    public async Task PurgingAllLinksForAReportStopsResolution()
    {
        var h = new Harness();
        var (orgId, projectId, reportId) = await h.SeedCaseAsync(passed: false);
        var (_, token) = await h.Service.CreateAsync(orgId, projectId, reportId, Guid.NewGuid());

        await h.Links.DeleteAllForReportAsync(reportId);

        await Assert.ThrowsAsync<ShareLinkUnavailableException>(() => h.Service.ResolveAsync(token));
    }

    [Fact]
    public async Task CreateForAnUnknownReportThrows()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ShareTargetNotFoundException>(
            () => h.Service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }
}
