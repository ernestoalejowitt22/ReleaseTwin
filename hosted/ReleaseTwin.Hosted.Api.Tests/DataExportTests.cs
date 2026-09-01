using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;
using ReleaseTwin.Hosted.Api.Services.DataExport;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ExportArchiveBuilderTests
{
    private sealed class Harness
    {
        public InMemoryHostedTable Table { get; } = new();
        public InMemoryEvidenceBlobStore Blobs { get; } = new();
        public OrganizationRepository Orgs { get; }
        public ProjectRepository Projects { get; }
        public CaseReportRepository CaseReports { get; }
        public FlagProofReportRepository FlagProofReports { get; }
        public RunEvidenceRepository Evidence { get; }
        public ExportArchiveBuilder Builder { get; }

        public Harness()
        {
            Orgs = new OrganizationRepository(Table);
            Projects = new ProjectRepository(Table);
            CaseReports = new CaseReportRepository(Table);
            FlagProofReports = new FlagProofReportRepository(Table);
            Evidence = new RunEvidenceRepository(Table);
            Builder = new ExportArchiveBuilder(Orgs, Projects, CaseReports, FlagProofReports, Evidence, Blobs);
        }

        public async Task<Guid> SeedOrgAsync(string name = "Acme")
        {
            var org = new Organization { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTimeOffset.UtcNow };
            await Table.PutItemAsync(OrganizationRepository.ToItem(org));
            return org.Id;
        }
    }

    private static Dictionary<string, string> ReadZip(byte[] zip, out Dictionary<string, byte[]> binary)
    {
        var text = new Dictionary<string, string>();
        binary = new Dictionary<string, byte[]>();
        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            var bytes = buf.ToArray();
            binary[entry.FullName] = bytes;
            if (entry.FullName.EndsWith(".json"))
            {
                text[entry.FullName] = Encoding.UTF8.GetString(bytes);
            }
        }
        return text;
    }

    [Fact]
    public async Task ArchiveContainsEveryReportAndEvidenceDocumentAcrossProjects()
    {
        var h = new Harness();
        var orgId = await h.SeedOrgAsync();
        var p1 = await h.Projects.CreateAsync(orgId, "orders");
        var p2 = await h.Projects.CreateAsync(orgId, "payments");

        var caseWithEvidence = new UploadedCaseReport
        {
            Id = Guid.NewGuid(), ProjectId = p1.Id, CaseId = "ORD-1", OracleLocator = "t/ORD-1",
            FixtureSha256 = "abc", Passed = false, Classification = "Assertion", FailureDetail = "status was pending",
            CleanupStatus = "AllSucceeded", DurationMs = 12, UploadedAt = DateTimeOffset.UtcNow,
        };
        var caseMetadataOnly = new UploadedCaseReport
        {
            Id = Guid.NewGuid(), ProjectId = p2.Id, CaseId = "PAY-9", OracleLocator = "t/PAY-9",
            FixtureSha256 = "def", Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 3, UploadedAt = DateTimeOffset.UtcNow,
        };
        await h.CaseReports.AddAsync(caseWithEvidence);
        await h.CaseReports.AddAsync(caseMetadataOnly);
        await h.FlagProofReports.AddAsync(new UploadedFlagProofReport
        {
            Id = Guid.NewGuid(), ProjectId = p1.Id, CaseId = "ORD-1", OracleLocator = "t/ORD-1",
            BuildIdentity = "b-1", Outcome = "Passed", KnownBadLegPassed = true, KnownGoodLegPassed = false,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await h.Blobs.PutAsync("shot-1", [1, 2, 3]);
        await h.Evidence.AddAsync(new UploadedRunEvidence
        {
            Id = Guid.NewGuid(), ProjectId = p1.Id, ReportId = caseWithEvidence.Id, ReportKind = "case",
            DocumentJson = """{"legs":[{"steps":[{"name":"POST /refunds"}]}]}""",
            ScreenshotIds = ["shot-1", "shot-gone"], UploadedAt = DateTimeOffset.UtcNow,
        });

        var zip = await h.Builder.BuildAsync(orgId);
        var json = ReadZip(zip, out var binary);

        // run-history has both projects' reports
        using var history = JsonDocument.Parse(json["run-history.json"]);
        var cases = history.RootElement.GetProperty("caseReports").EnumerateArray().ToList();
        Assert.Equal(2, cases.Count);
        Assert.Contains(cases, c => c.GetProperty("caseId").GetString() == "ORD-1" && c.GetProperty("projectName").GetString() == "orders");
        Assert.Contains(cases, c => c.GetProperty("caseId").GetString() == "PAY-9" && c.GetProperty("projectName").GetString() == "payments");
        Assert.Single(history.RootElement.GetProperty("flagProofReports").EnumerateArray());

        // evidence document is verbatim, only for the report that has one
        Assert.True(json.ContainsKey($"evidence/{caseWithEvidence.Id}.json"));
        Assert.False(json.ContainsKey($"evidence/{caseMetadataOnly.Id}.json"));
        using var ev = JsonDocument.Parse(json[$"evidence/{caseWithEvidence.Id}.json"]);
        Assert.Equal("POST /refunds", ev.RootElement.GetProperty("document").GetProperty("legs")[0].GetProperty("steps")[0].GetProperty("name").GetString());

        // screenshot present; the missing one recorded, not written
        Assert.Equal(new byte[] { 1, 2, 3 }, binary["screenshots/shot-1.png"]);
        Assert.False(binary.ContainsKey("screenshots/shot-gone.png"));

        using var manifest = JsonDocument.Parse(json["manifest.json"]);
        Assert.Equal(1, manifest.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal("Acme", manifest.RootElement.GetProperty("organization").GetProperty("name").GetString());
        Assert.Equal(2, manifest.RootElement.GetProperty("counts").GetProperty("caseReports").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("counts").GetProperty("evidenceDocuments").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("counts").GetProperty("screenshots").GetInt32());
        Assert.Equal("shot-gone", manifest.RootElement.GetProperty("missingScreenshots").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task ArchiveIsScopedToOneOrganization()
    {
        var h = new Harness();
        var orgA = await h.SeedOrgAsync("A");
        var orgB = await h.SeedOrgAsync("B");
        var pa = await h.Projects.CreateAsync(orgA, "a-proj");
        var pb = await h.Projects.CreateAsync(orgB, "b-proj");
        await h.CaseReports.AddAsync(new UploadedCaseReport { Id = Guid.NewGuid(), ProjectId = pa.Id, CaseId = "A-CASE", OracleLocator = "t", FixtureSha256 = "x", Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 1, UploadedAt = DateTimeOffset.UtcNow });
        await h.CaseReports.AddAsync(new UploadedCaseReport { Id = Guid.NewGuid(), ProjectId = pb.Id, CaseId = "B-CASE", OracleLocator = "t", FixtureSha256 = "x", Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 1, UploadedAt = DateTimeOffset.UtcNow });

        var zip = await h.Builder.BuildAsync(orgA);
        var json = ReadZip(zip, out _);

        Assert.Contains("A-CASE", json["run-history.json"]);
        Assert.DoesNotContain("B-CASE", json["run-history.json"]);
        Assert.DoesNotContain(pb.Id.ToString(), string.Join("\n", json.Values));
    }

    [Fact]
    public void RunHistoryFieldNamesMatchTheIngestContract()
    {
        // Shape-check: the export row records carry exactly the entity's own fields (renamed Id ->
        // reportId, plus projectId/projectName) so the format doesn't silently drift.
        var caseFields = typeof(UploadedCaseReport).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet();
        var exportCaseFields = typeof(ExportCaseReport).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Where(n => n != "EqualityContract").ToHashSet();
        var extra = exportCaseFields.Except(caseFields).Except(["ReportId", "ProjectName"]).ToList();
        var dropped = caseFields.Except(exportCaseFields).Except(["Id"]).ToList();
        Assert.Empty(extra);
        Assert.Empty(dropped);
    }

    [Fact]
    public async Task ArchiveContainsNoSecretShapedData()
    {
        var h = new Harness();
        var orgId = await h.SeedOrgAsync();
        // Give the org a project + a token + adapter cred + project secret + a Polar id.
        var project = await h.Projects.CreateAsync(orgId, "p");
        await new ApiTokenRepository(h.Table).CreateAsync(project.Id, orgId, "TOKENHASH-SECRET", "rtw_1234");
        await new AdapterCredentialRepository(h.Table).SetAsync(project.Id, "azdo", "ENCRYPTED-CRED-BLOB", "u", "U");
        await new ProjectSecretRepository(h.Table).SetAsync(project.Id, "API_KEY", "ENCRYPTED-SECRET-BLOB", "u", "U");
        await h.Orgs.SetBillingAsync(orgId, BillingStatus.Active, DateTimeOffset.UtcNow, BillingCadence.Monthly, "cus_SECRET", "sub_SECRET");
        await h.CaseReports.AddAsync(new UploadedCaseReport { Id = Guid.NewGuid(), ProjectId = project.Id, CaseId = "C", OracleLocator = "t", FixtureSha256 = "x", Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 1, UploadedAt = DateTimeOffset.UtcNow });

        var zip = await h.Builder.BuildAsync(orgId);
        var all = Encoding.UTF8.GetString(zip);

        foreach (var secret in new[] { "TOKENHASH-SECRET", "rtw_1234", "ENCRYPTED-CRED-BLOB", "ENCRYPTED-SECRET-BLOB", "cus_SECRET", "sub_SECRET" })
        {
            Assert.DoesNotContain(secret, all);
        }
    }
}

public class ExportEndpointTests
{
    private static async Task<Guid> SeedOrgWithDataAsync(CustomWebApplicationFactory factory)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        var project = await new ProjectRepository(table).CreateAsync(org.Id, "p");
        await new CaseReportRepository(table).AddAsync(new UploadedCaseReport
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, CaseId = "C-1", OracleLocator = "t", FixtureSha256 = "x",
            Passed = true, CleanupStatus = "AllSucceeded", DurationMs = 1, UploadedAt = DateTimeOffset.UtcNow,
        });
        return org.Id;
    }

    [Fact]
    public async Task AdminGetsAZipInTheDevPath()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var orgId = await SeedOrgWithDataAsync(factory);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);

        var response = await admin.PostAsync("/api/export", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, e => e.FullName == "manifest.json");
        Assert.Contains(archive.Entries, e => e.FullName == "run-history.json");
    }

    [Theory]
    [InlineData(MembershipRole.Member)]
    [InlineData(MembershipRole.Viewer)]
    public async Task NonAdminIsForbidden(MembershipRole role)
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var orgId = await SeedOrgWithDataAsync(factory);
        var client = factory.CreateClientForOrg(orgId, role);

        var response = await client.PostAsync("/api/export", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
