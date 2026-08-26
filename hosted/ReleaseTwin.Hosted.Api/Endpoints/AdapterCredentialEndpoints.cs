using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-adapter-credentials: web-session-authenticated set/list/revoke for the dashboard — same
/// org-scoping discipline as every other dashboard endpoint. Values are never returned once set;
/// only metadata (which adapters are configured, by whom, when).
/// </summary>
public static class AdapterCredentialEndpoints
{
    public static void MapAdapterCredentialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/adapter-credentials")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("/{projectId:guid}", async (Guid projectId, AdapterCredentialService credentials, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var stored = await credentials.ListMetadataAsync(projectId);
            return Results.Ok(stored.Select(c => new { c.Adapter, c.LastSetByDisplayName, c.UpdatedAt }));
        });

        group.MapPut("/{projectId:guid}/{adapter}", async (
            Guid projectId, string adapter, SetAdapterCredentialRequest request,
            AdapterCredentialService credentials, IProjectRepository projects, CurrentOrganizationAccessor currentOrg, ClaimsPrincipal user) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var userId = user.FindFirstValue("user_id") ?? throw new InvalidOperationException("Authenticated request is missing a user_id claim.");
            var displayName = user.FindFirstValue("user_display_name") ?? userId;

            var result = await credentials.SetAsync(projectId, adapter, request.Fields ?? new Dictionary<string, string>(), userId, displayName);
            if (result.UnknownAdapter)
            {
                return Results.NotFound($"Unknown adapter '{adapter}'.");
            }

            if (!result.Success)
            {
                return Results.BadRequest(new { error = "incomplete-fields", missing = result.MissingFields });
            }

            return Results.NoContent();
        });

        group.MapDelete("/{projectId:guid}/{adapter}", async (Guid projectId, string adapter, AdapterCredentialService credentials, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            await credentials.DeleteAsync(projectId, adapter);
            return Results.NoContent();
        });
    }
}

public sealed record SetAdapterCredentialRequest(Dictionary<string, string>? Fields);
