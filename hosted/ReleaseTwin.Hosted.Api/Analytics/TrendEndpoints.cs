using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Analytics;

/// <summary>
/// trend-analytics: read-only, web-session-authenticated trend endpoints for the dashboard — same
/// org-scoping discipline as every other dashboard endpoint (caller's organization only, never a
/// client-chosen id). Gated on the <c>trendAnalytics</c> entitlement; an unentitled organization
/// gets the standard entitlement-required response and no series data.
/// </summary>
public static class TrendEndpoints
{
    public static void MapTrendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("/projects/{projectId:guid}/trends", async (
            Guid projectId, string? window,
            TrendService trends, IProjectRepository projects, IOrganizationRepository organizations,
            IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            if (!TrendWindowParsing.TryParse(window ?? "30d", out var parsed))
            {
                return Results.BadRequest(new { error = "invalid-window", allowed = new[] { "7d", "30d", "90d" } });
            }

            if (RefuseUnentitled(entitlements.For(await organizations.GetAsync(orgId.Value))) is { } refusal)
            {
                return refusal;
            }

            return Results.Ok(await trends.ForProjectAsync(projectId, parsed, DateTimeOffset.UtcNow));
        });

        group.MapGet("/trends", async (
            string? window,
            TrendService trends, IOrganizationRepository organizations,
            IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            if (!TrendWindowParsing.TryParse(window ?? "30d", out var parsed))
            {
                return Results.BadRequest(new { error = "invalid-window", allowed = new[] { "7d", "30d", "90d" } });
            }

            if (RefuseUnentitled(entitlements.For(await organizations.GetAsync(orgId.Value))) is { } refusal)
            {
                return refusal;
            }

            return Results.Ok(await trends.ForOrganizationAsync(orgId.Value, parsed, DateTimeOffset.UtcNow));
        });
    }

    private static IResult? RefuseUnentitled(Entitlements entitlements) =>
        entitlements.TrendAnalytics
            ? null
            // plan-catalog-and-entitlements convention: distinct error code + the missing entitlement
            // key so the frontend shows the right upgrade prompt rather than a generic error.
            : Results.Json(new { error = "entitlement-required", entitlement = "trendAnalytics" }, statusCode: StatusCodes.Status403Forbidden);
}
