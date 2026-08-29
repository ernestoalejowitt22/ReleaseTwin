using System.Text;
using System.Text.Json;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record UploadedScreenshot(string Id, byte[] Bytes);

/// <summary>
/// evidence-capture / evidence-store: validates and stores an optional, already-redacted evidence
/// document that rides along with an ingest report. Never inspects the document's contents (ingest-api
/// spec) — only its size — and stores it only for Paid-tier organizations.
/// </summary>
public sealed class EvidenceIngestService
{
    public const int MaxDocumentBytes = 256 * 1024;
    public const int MaxScreenshots = 20;
    public const int MaxScreenshotBytes = 2 * 1024 * 1024;

    private readonly IRunEvidenceRepository _evidence;
    private readonly IEvidenceBlobStore _blobs;
    private readonly IOrganizationRepository _organizations;

    public EvidenceIngestService(IRunEvidenceRepository evidence, IEvidenceBlobStore blobs, IOrganizationRepository organizations)
    {
        _evidence = evidence;
        _blobs = blobs;
        _organizations = organizations;
    }

    /// <summary>
    /// True ⇒ the payload is within limits and may proceed to storing the report. False ⇒ the whole
    /// request must be rejected atomically (nothing stored). <paramref name="rejectionReason"/> is
    /// set on rejection.
    /// </summary>
    public bool IsWithinLimits(JsonElement? evidence, IReadOnlyList<UploadedScreenshot> screenshots, out string? rejectionReason)
    {
        rejectionReason = null;
        if (evidence is null)
        {
            return true;
        }

        if (Encoding.UTF8.GetByteCount(evidence.Value.GetRawText()) > MaxDocumentBytes)
        {
            rejectionReason = $"evidence document exceeds the {MaxDocumentBytes / 1024} KB limit";
            return false;
        }

        if (screenshots.Count > MaxScreenshots)
        {
            rejectionReason = $"more than {MaxScreenshots} screenshots";
            return false;
        }

        if (screenshots.Any(s => s.Bytes.Length > MaxScreenshotBytes))
        {
            rejectionReason = $"a screenshot exceeds the {MaxScreenshotBytes / 1024 / 1024} MB limit";
            return false;
        }

        return true;
    }

    /// <summary>Stores the evidence for a just-stored report. Returns whether it was accepted (false ⇒ Free tier — report kept, evidence dropped).</summary>
    public async Task<bool> StoreAsync(
        Guid organizationId, Guid projectId, Guid reportId, string reportKind,
        JsonElement evidence, IReadOnlyList<UploadedScreenshot> screenshots, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization?.PlanTier != PlanTier.Paid)
        {
            return false;
        }

        var ids = new List<string>(screenshots.Count);
        foreach (var screenshot in screenshots)
        {
            await _blobs.PutAsync(screenshot.Id, screenshot.Bytes, cancellationToken);
            ids.Add(screenshot.Id);
        }

        await _evidence.AddAsync(new UploadedRunEvidence
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReportId = reportId,
            ReportKind = reportKind,
            DocumentJson = evidence.GetRawText(),
            ScreenshotIds = ids,
            UploadedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return true;
    }
}
