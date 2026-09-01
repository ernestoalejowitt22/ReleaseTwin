using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Services.DataExport;

/// <summary>
/// data-export: builds a self-describing ZIP archive of one organization's full run history and stored
/// evidence — the continuity commitment made on the marketing security page. Reads only per-project
/// lists scoped to the given organization; carries only the metadata the ingest contract already
/// accepts plus evidence documents already redacted by the customer's CLI. See <c>docs/data-export.md</c>.
/// </summary>
public sealed class ExportArchiveBuilder
{
    /// <summary>Bumped only on a breaking change to the archive layout; documented in <c>docs/data-export.md</c>.</summary>
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IRunEvidenceRepository _evidence;
    private readonly IEvidenceBlobStore _blobs;

    public ExportArchiveBuilder(
        IOrganizationRepository organizations,
        IProjectRepository projects,
        ICaseReportRepository caseReports,
        IFlagProofReportRepository flagProofReports,
        IRunEvidenceRepository evidence,
        IEvidenceBlobStore blobs)
    {
        _organizations = organizations;
        _projects = projects;
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _evidence = evidence;
        _blobs = blobs;
    }

    public async Task<byte[]> BuildAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var projects = await _projects.ListByOrganizationAsync(organizationId, cancellationToken);
        var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);

        var caseRows = new List<ExportCaseReport>();
        var flagProofRows = new List<ExportFlagProofReport>();
        var evidenceDocs = new List<Data.Entities.UploadedRunEvidence>();

        foreach (var project in projects)
        {
            var name = projectNames[project.Id];

            foreach (var r in await _caseReports.ListByProjectAsync(project.Id, cancellationToken))
            {
                caseRows.Add(new ExportCaseReport(
                    r.Id, project.Id, name, r.CaseId, r.OracleLocator, r.FixtureSha256, r.Passed,
                    r.Classification, r.FailureDetail, r.Release, r.CleanupStatus, r.DurationMs, r.UploadedAt));
            }

            foreach (var r in await _flagProofReports.ListByProjectAsync(project.Id, cancellationToken))
            {
                flagProofRows.Add(new ExportFlagProofReport(
                    r.Id, project.Id, name, r.CaseId, r.OracleLocator, r.BuildIdentity, r.Outcome,
                    r.KnownBadLegPassed, r.KnownGoodLegPassed, r.Release, r.UploadedAt));
            }

            evidenceDocs.AddRange(await _evidence.ListByProjectAsync(project.Id, cancellationToken));
        }

        var missingScreenshots = new List<string>();

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJson(zip, "run-history.json", new { caseReports = caseRows, flagProofReports = flagProofRows });

            foreach (var doc in evidenceDocs)
            {
                using var docJson = JsonDocument.Parse(doc.DocumentJson);
                WriteJson(zip, $"evidence/{doc.ReportId}.json", new
                {
                    reportId = doc.ReportId,
                    reportKind = doc.ReportKind,
                    uploadedAt = doc.UploadedAt,
                    screenshotIds = doc.ScreenshotIds,
                    document = docJson.RootElement.Clone(),
                });
            }

            var screenshotIds = evidenceDocs.SelectMany(d => d.ScreenshotIds).Distinct().ToList();
            var writtenScreenshots = 0;
            foreach (var id in screenshotIds)
            {
                var bytes = await _blobs.GetAsync(id, cancellationToken);
                if (bytes is null)
                {
                    missingScreenshots.Add(id);
                    continue;
                }

                var entry = zip.CreateEntry($"screenshots/{Sanitize(id)}.png", CompressionLevel.NoCompression);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(bytes, cancellationToken);
                writtenScreenshots++;
            }

            WriteJson(zip, "manifest.json", new
            {
                formatVersion = FormatVersion,
                generatedAt = DateTimeOffset.UtcNow,
                organization = new { id = organizationId, name = organization?.Name },
                counts = new
                {
                    caseReports = caseRows.Count,
                    flagProofReports = flagProofRows.Count,
                    evidenceDocuments = evidenceDocs.Count,
                    screenshots = writtenScreenshots,
                },
                missingScreenshots,
            });
        }

        return buffer.ToArray();
    }

    private static void WriteJson(ZipArchive zip, string path, object value)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)));
    }

    private static string Sanitize(string id) =>
        new(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
}

/// <summary>data-export: the run-history row shapes — every field named as the ingest contract's
/// <c>UploadedCaseReport</c> / <c>UploadedFlagProofReport</c>, plus the owning project. A shape-check
/// test pins these against the entity records.</summary>
public sealed record ExportCaseReport(
    Guid ReportId, Guid ProjectId, string ProjectName, string CaseId, string OracleLocator,
    string FixtureSha256, bool Passed, string? Classification, string? FailureDetail, string? Release,
    string CleanupStatus, long DurationMs, DateTimeOffset UploadedAt);

public sealed record ExportFlagProofReport(
    Guid ReportId, Guid ProjectId, string ProjectName, string CaseId, string OracleLocator,
    string BuildIdentity, string Outcome, bool? KnownBadLegPassed, bool? KnownGoodLegPassed,
    string? Release, DateTimeOffset UploadedAt);
