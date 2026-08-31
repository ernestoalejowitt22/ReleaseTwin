using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// ingest-api spec: token-authenticated, reports stored scoped to the token's own project, malformed
/// payloads rejected atomically. usage-metering: also atomically increments the uploading org's usage
/// counter for the current period.
///
/// evidence-capture: a request MAY additionally carry an already-redacted evidence document — either
/// as an <c>evidence</c> property on the JSON body, or (when it has screenshots) as a multipart form
/// with a <c>report</c> JSON part and <c>screenshot:{id}</c> file parts. The document is stored
/// opaquely and only for Paid-tier organizations; an oversize document rejects the whole request
/// with nothing stored.
/// </summary>
public static class IngestEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        group.MapPost("/case-report", async (HttpRequest http, ICaseReportRepository reports, IUsageCounterRepository usage, EvidenceIngestService evidenceIngest, ClaimsPrincipal user) =>
        {
            var (request, screenshots, bindError) = await ReadAsync<IngestCaseReportRequest>(http);
            if (bindError is not null)
            {
                return bindError;
            }

            if (string.IsNullOrWhiteSpace(request!.CaseId) || string.IsNullOrWhiteSpace(request.OracleLocator)
                || string.IsNullOrWhiteSpace(request.FixtureSha256) || string.IsNullOrWhiteSpace(request.CleanupStatus))
            {
                return Results.BadRequest("Missing one or more required fields.");
            }

            if (!evidenceIngest.IsWithinLimits(request.Evidence, screenshots, out var reason))
            {
                // ingest-api spec: oversize evidence rejects the entire request atomically — nothing stored.
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            if (!TryNormalizeRelease(request.Release, out var caseRelease))
            {
                return Results.BadRequest("The 'release' label is too long.");
            }

            var projectId = GetProjectId(user);
            var organizationId = GetOrganizationId(user);
            var entity = new UploadedCaseReport
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CaseId = request.CaseId,
                OracleLocator = request.OracleLocator,
                FixtureSha256 = request.FixtureSha256,
                Passed = request.Passed,
                Classification = request.Classification,
                FailureDetail = request.FailureDetail,
                Release = caseRelease,
                CleanupStatus = request.CleanupStatus,
                DurationMs = request.DurationMs,
                UploadedAt = DateTimeOffset.UtcNow,
            };

            await reports.AddAsync(entity);
            await usage.IncrementAsync(organizationId, Keys.CurrentUtcPeriod(), isFlagProof: false);

            if (request.Evidence is null)
            {
                return Results.Created($"/api/reports/case/{entity.Id}", new { entity.Id });
            }

            var accepted = await evidenceIngest.StoreAsync(organizationId, projectId, entity.Id, "case", request.Evidence.Value, screenshots, http.HttpContext.RequestAborted);
            return Results.Created($"/api/reports/case/{entity.Id}", new { entity.Id, evidenceAccepted = accepted });
        });

        group.MapPost("/flag-proof-report", async (HttpRequest http, IFlagProofReportRepository reports, IUsageCounterRepository usage, EvidenceIngestService evidenceIngest, ClaimsPrincipal user) =>
        {
            var (request, screenshots, bindError) = await ReadAsync<IngestFlagProofReportRequest>(http);
            if (bindError is not null)
            {
                return bindError;
            }

            if (string.IsNullOrWhiteSpace(request!.CaseId) || string.IsNullOrWhiteSpace(request.OracleLocator)
                || string.IsNullOrWhiteSpace(request.BuildIdentity) || string.IsNullOrWhiteSpace(request.Outcome))
            {
                return Results.BadRequest("Missing one or more required fields.");
            }

            if (!evidenceIngest.IsWithinLimits(request.Evidence, screenshots, out _))
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            if (!TryNormalizeRelease(request.Release, out var flagProofRelease))
            {
                return Results.BadRequest("The 'release' label is too long.");
            }

            var projectId = GetProjectId(user);
            var organizationId = GetOrganizationId(user);
            var entity = new UploadedFlagProofReport
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CaseId = request.CaseId,
                OracleLocator = request.OracleLocator,
                BuildIdentity = request.BuildIdentity,
                Outcome = request.Outcome,
                KnownBadLegPassed = request.KnownBadLegPassed,
                KnownGoodLegPassed = request.KnownGoodLegPassed,
                Release = flagProofRelease,
                UploadedAt = DateTimeOffset.UtcNow,
            };

            await reports.AddAsync(entity);
            await usage.IncrementAsync(organizationId, Keys.CurrentUtcPeriod(), isFlagProof: true);

            if (request.Evidence is null)
            {
                return Results.Created($"/api/reports/flag-proof/{entity.Id}", new { entity.Id });
            }

            var accepted = await evidenceIngest.StoreAsync(organizationId, projectId, entity.Id, "flag-proof", request.Evidence.Value, screenshots, http.HttpContext.RequestAborted);
            return Results.Created($"/api/reports/flag-proof/{entity.Id}", new { entity.Id, evidenceAccepted = accepted });
        });
    }

    private static async Task<(T? Request, IReadOnlyList<UploadedScreenshot> Screenshots, IResult? Error)> ReadAsync<T>(HttpRequest http)
        where T : class
    {
        try
        {
            if (http.HasFormContentType && http.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            {
                var form = await http.ReadFormAsync();
                var reportJson = form["report"].ToString();
                if (string.IsNullOrWhiteSpace(reportJson))
                {
                    return (null, Array.Empty<UploadedScreenshot>(), Results.BadRequest("multipart upload is missing its 'report' part."));
                }

                var request = JsonSerializer.Deserialize<T>(reportJson, JsonOptions);
                if (request is null)
                {
                    return (null, Array.Empty<UploadedScreenshot>(), Results.BadRequest("multipart 'report' part is not a valid report."));
                }

                var screenshots = new List<UploadedScreenshot>();
                foreach (var file in form.Files)
                {
                    if (!file.Name.StartsWith("screenshot:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var id = file.Name["screenshot:".Length..];
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    screenshots.Add(new UploadedScreenshot(id, ms.ToArray()));
                }

                return (request, screenshots, null);
            }

            var body = await http.ReadFromJsonAsync<T>(JsonOptions);
            return body is null
                ? (null, Array.Empty<UploadedScreenshot>(), Results.BadRequest("Request body is empty."))
                : (body, Array.Empty<UploadedScreenshot>(), null);
        }
        catch (JsonException)
        {
            return (null, Array.Empty<UploadedScreenshot>(), Results.BadRequest("Request body is not valid JSON."));
        }
    }

    /// <summary>release-readiness-rollup: trims the label, treats blank as absent, and caps length (design.md D-E) — the label is an opaque grouping id, not free text.</summary>
    private const int MaxReleaseLength = 200;

    private static bool TryNormalizeRelease(string? raw, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        return normalized is null || normalized.Length <= MaxReleaseLength;
    }

    private static Guid GetProjectId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim)
            ?? throw new InvalidOperationException("Authenticated principal is missing a project_id claim.");
        return Guid.Parse(claim);
    }

    private static Guid GetOrganizationId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ApiTokenDefaults.OrganizationIdClaim)
            ?? throw new InvalidOperationException("Authenticated principal is missing an organization_id claim.");
        return Guid.Parse(claim);
    }
}
