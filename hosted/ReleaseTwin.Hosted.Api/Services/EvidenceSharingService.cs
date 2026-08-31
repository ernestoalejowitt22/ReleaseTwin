using System.Text.Json;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Flags;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// evidence-sharing (design D7): create / list / revoke per-run share links, and resolve a link token
/// to a <see cref="SharedEvidenceView"/> — the narrow projection that is the security boundary. This
/// service is the only place that maps a token to any stored data.
/// </summary>
public sealed class EvidenceSharingService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

    private readonly IShareLinkRepository _links;
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IRunEvidenceRepository _evidence;
    private readonly IOrganizationRepository _organizations;
    private readonly IEntitlementService _entitlements;
    private readonly IFlagService _flags;
    private readonly ITokenService _tokens;

    public EvidenceSharingService(
        IShareLinkRepository links,
        ICaseReportRepository caseReports,
        IFlagProofReportRepository flagProofReports,
        IRunEvidenceRepository evidence,
        IOrganizationRepository organizations,
        IEntitlementService entitlements,
        IFlagService flags,
        ITokenService tokens)
    {
        _links = links;
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _evidence = evidence;
        _organizations = organizations;
        _entitlements = entitlements;
        _tokens = tokens;
        _flags = flags;
    }

    public sealed record ShareLinkSummary(Guid Id, ShareLinkState State, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

    public async Task<(ShareLink Link, string Token)> CreateAsync(Guid organizationId, Guid projectId, Guid reportId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var (caseId, kind, result, classification, fixtureSha) = await LoadReportFactsAsync(projectId, reportId, cancellationToken)
            ?? throw new ShareTargetNotFoundException();

        var token = ShareLinkRepository.NewToken(reportId);
        var link = new ShareLink
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ProjectId = projectId,
            OrganizationId = organizationId,
            ReportKind = kind,
            TokenHash = _tokens.Hash(token),
            State = ShareLinkState.Active,
            ExpiresAt = DateTimeOffset.UtcNow.Add(DefaultLifetime),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = createdByUserId,
            CaseId = caseId,
            Result = result,
            Classification = classification,
            FixtureSha256 = fixtureSha,
        };
        await _links.PutAsync(link, cancellationToken);
        return (link, token);
    }

    public async Task<IReadOnlyList<ShareLinkSummary>> ListAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        (await _links.ListByReportAsync(reportId, cancellationToken))
            .Select(l => new ShareLinkSummary(l.Id, l.State, l.ExpiresAt, l.CreatedAt))
            .ToList();

    public Task RevokeAsync(Guid reportId, Guid linkId, CancellationToken cancellationToken = default) =>
        _links.RevokeAsync(reportId, linkId, cancellationToken);

    /// <summary>Resolves a link token to the shared view, applying every gate (existence, state/expiry,
    /// global flag, current org entitlement). Throws <see cref="ShareLinkUnavailableException"/> when the
    /// link cannot be shown for any reason other than a lost entitlement, and
    /// <see cref="ShareEntitlementRevokedException"/> for that case (so the caller can 403 vs 404).</summary>
    public async Task<SharedEvidenceView> ResolveAsync(string token, CancellationToken cancellationToken = default)
    {
        var link = await LoadResolvableLinkAsync(token, cancellationToken);
        var stored = await _evidence.GetByReportAsync(link.ProjectId, link.ReportId, cancellationToken);

        JsonElement? document = null;
        if (stored is not null)
        {
            using var doc = JsonDocument.Parse(stored.DocumentJson);
            document = doc.RootElement.Clone();
        }

        return new SharedEvidenceView(
            CaseId: link.CaseId,
            ReportKind: link.ReportKind,
            Result: link.Result,
            Classification: link.Classification,
            FixtureSha256: link.FixtureSha256,
            HasEvidenceDocument: stored is not null,
            EvidenceUploadedAt: stored?.UploadedAt,
            Document: document,
            ScreenshotIds: stored?.ScreenshotIds ?? Array.Empty<string>());
    }

    public async Task<(byte[]? Bytes, string ContentType)> ResolveScreenshotAsync(string token, string screenshotId, IEvidenceBlobStore blobs, CancellationToken cancellationToken = default)
    {
        var link = await LoadResolvableLinkAsync(token, cancellationToken);
        var stored = await _evidence.GetByReportAsync(link.ProjectId, link.ReportId, cancellationToken);
        if (stored is null || !stored.ScreenshotIds.Contains(screenshotId))
        {
            return (null, "");
        }

        return (await blobs.GetAsync(screenshotId, cancellationToken), "image/png");
    }

    private async Task<ShareLink> LoadResolvableLinkAsync(string token, CancellationToken cancellationToken)
    {
        if (!await _flags.GetBooleanAsync("evidence-sharing", cancellationToken: cancellationToken)
            || !ShareLinkRepository.TryParseReportId(token, out var reportId))
        {
            throw new ShareLinkUnavailableException();
        }

        var link = await _links.GetByTokenHashAsync(reportId, _tokens.Hash(token), cancellationToken);
        if (link is null || !link.IsResolvable(DateTimeOffset.UtcNow))
        {
            throw new ShareLinkUnavailableException();
        }

        var org = await _organizations.GetAsync(link.OrganizationId, cancellationToken);
        if (org is null || !_entitlements.For(org).EvidenceSharing)
        {
            throw new ShareEntitlementRevokedException();
        }

        return link;
    }

    private async Task<(string CaseId, string Kind, string Result, string? Classification, string FixtureSha256)?> LoadReportFactsAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken)
    {
        var caseReport = (await _caseReports.ListByProjectAsync(projectId, cancellationToken)).FirstOrDefault(r => r.Id == reportId);
        if (caseReport is not null)
        {
            return (caseReport.CaseId, "case", caseReport.Passed ? "passed" : "failed", caseReport.Classification, caseReport.FixtureSha256);
        }

        var flagProof = (await _flagProofReports.ListByProjectAsync(projectId, cancellationToken)).FirstOrDefault(r => r.Id == reportId);
        if (flagProof is not null)
        {
            return (flagProof.CaseId, "flag-proof", flagProof.Outcome.ToLowerInvariant(), null, flagProof.OracleLocator);
        }

        return null;
    }
}

/// <summary>
/// evidence-sharing (design D7): the ONLY data a share-link viewer sees. Deliberately flat and
/// self-contained — it carries the redacted evidence document, the run's result/classification, and
/// the fixture hash, and nothing that identifies or links to the organization, the project, other
/// runs, or any dashboard surface. <see cref="EvidenceSharingViewShapeTests"/> enforces this by
/// reflection.
/// </summary>
public sealed record SharedEvidenceView(
    string CaseId,
    string ReportKind,
    string Result,
    string? Classification,
    string FixtureSha256,
    bool HasEvidenceDocument,
    DateTimeOffset? EvidenceUploadedAt,
    JsonElement? Document,
    IReadOnlyList<string> ScreenshotIds);

public sealed class ShareTargetNotFoundException : Exception;

public sealed class ShareLinkUnavailableException : Exception;

public sealed class ShareEntitlementRevokedException : Exception;
