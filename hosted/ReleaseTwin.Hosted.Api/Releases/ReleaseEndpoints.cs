using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Releases;

/// <summary>
/// release-readiness-rollup: read-only, web-session-authenticated release endpoints for the
/// dashboard — same org-scoping discipline as every other dashboard endpoint (caller's organization
/// only, project ownership checked). Gated on the <c>releaseRollup</c> entitlement; an unentitled
/// organization gets the standard entitlement-required response.
/// </summary>
public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/releases")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("", async (
            Guid projectId,
            ReleaseRollupService rollups, IProjectRepository projects, IOrganizationRepository organizations,
            IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var guard = await GuardAsync(projectId, projects, organizations, entitlements, currentOrg);
            if (guard is not null)
            {
                return guard;
            }

            return Results.Ok(await rollups.ListReleasesAsync(projectId));
        });

        group.MapGet("/{label}", async (
            Guid projectId, string label, string? window,
            ReleaseRollupService rollups, IProjectRepository projects, IOrganizationRepository organizations,
            IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            if (!ReleaseWindowParsing.TryParse(window ?? "14d", out var windowDays))
            {
                return Results.BadRequest(new { error = "invalid-window", allowed = new[] { "7d", "14d", "30d", "90d" } });
            }

            var guard = await GuardAsync(projectId, projects, organizations, entitlements, currentOrg);
            if (guard is not null)
            {
                return guard;
            }

            return Results.Ok(await rollups.RollupAsync(projectId, label, windowDays, DateTimeOffset.UtcNow));
        });
    }

    /// <summary>Returns a refusal result, or null when the caller may proceed: org resolved, project owned, entitlement held.</summary>
    private static async Task<IResult?> GuardAsync(
        Guid projectId, IProjectRepository projects, IOrganizationRepository organizations,
        IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg)
    {
        var orgId = currentOrg.OrganizationId;
        if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
        {
            return Results.Forbid();
        }

        if (!entitlements.For(await organizations.GetAsync(orgId.Value)).ReleaseRollup)
        {
            // plan-catalog-and-entitlements convention: distinct error code + the missing entitlement
            // key so the frontend shows the right upgrade prompt rather than a generic error.
            return Results.Json(new { error = "entitlement-required", entitlement = "releaseRollup" }, statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }
}
