using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// ingest-api spec: token-authenticated, reports stored scoped to the token's own project, malformed
/// payloads rejected atomically (ASP.NET Core's own model binding/validation handles the "reject
/// before any data is stored" requirement — a payload that doesn't bind never reaches the handler).
/// </summary>
public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        group.MapPost("/case-report", async (IngestCaseReportRequest request, HostedDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseId) || string.IsNullOrWhiteSpace(request.OracleLocator)
                || string.IsNullOrWhiteSpace(request.FixtureSha256) || string.IsNullOrWhiteSpace(request.CleanupStatus))
            {
                return Results.BadRequest("Missing one or more required fields.");
            }

            var projectId = GetProjectId(user);
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
                CleanupStatus = request.CleanupStatus,
                DurationMs = request.DurationMs,
                UploadedAt = DateTimeOffset.UtcNow,
            };

            db.UploadedCaseReports.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/reports/case/{entity.Id}", new { entity.Id });
        });

        group.MapPost("/flag-proof-report", async (IngestFlagProofReportRequest request, HostedDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseId) || string.IsNullOrWhiteSpace(request.OracleLocator)
                || string.IsNullOrWhiteSpace(request.BuildIdentity) || string.IsNullOrWhiteSpace(request.Outcome))
            {
                return Results.BadRequest("Missing one or more required fields.");
            }

            var projectId = GetProjectId(user);
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
                UploadedAt = DateTimeOffset.UtcNow,
            };

            db.UploadedFlagProofReports.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/reports/flag-proof/{entity.Id}", new { entity.Id });
        });
    }

    private static Guid GetProjectId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim)
            ?? throw new InvalidOperationException("Authenticated principal is missing a project_id claim.");
        return Guid.Parse(claim);
    }
}
