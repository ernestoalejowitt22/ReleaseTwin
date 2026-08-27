using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-project-secrets: web-session-authenticated set/list/revoke for the dashboard — same
/// org-scoping discipline as every other dashboard endpoint. Values are never returned once set;
/// only metadata (which names are configured, by whom, when).
/// </summary>
public static class ProjectSecretEndpoints
{
    public static void MapProjectSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/project-secrets")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("/{projectId:guid}", async (Guid projectId, ProjectSecretService secrets, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var stored = await secrets.ListMetadataAsync(projectId);
            return Results.Ok(stored.Select(s => new { s.Name, s.LastSetByDisplayName, s.UpdatedAt }));
        });

        group.MapPut("/{projectId:guid}/{name}", async (
            Guid projectId, string name, SetProjectSecretRequest request,
            ProjectSecretService secrets, IProjectRepository projects, CurrentOrganizationAccessor currentOrg, ClaimsPrincipal user) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Value))
            {
                return Results.BadRequest(new { error = "value is required" });
            }

            var userId = user.FindFirstValue("user_id") ?? throw new InvalidOperationException("Authenticated request is missing a user_id claim.");
            var displayName = user.FindFirstValue("user_display_name") ?? userId;

            try
            {
                await secrets.SetAsync(orgId.Value, projectId, name, request.Value, userId, displayName);
                return Results.NoContent();
            }
            catch (PaidTierRequiredException)
            {
                // plan-tier-gating convention: a distinct error code, not a generic 400/500, so the
                // frontend can show the right message and upgrade prompt.
                return Results.Json(new { error = "paid-tier-required" }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapDelete("/{projectId:guid}/{name}", async (Guid projectId, string name, ProjectSecretService secrets, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            await secrets.DeleteAsync(projectId, name);
            return Results.NoContent();
        });
    }
}

public sealed record SetProjectSecretRequest(string? Value);
