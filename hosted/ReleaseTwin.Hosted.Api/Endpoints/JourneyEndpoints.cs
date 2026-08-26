using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-journeys / dashboard delta: web-session-authenticated create/list/read for the visual
/// builder — same org-scoping discipline as every other dashboard endpoint (a journey is reachable
/// only through a project the signed-in organization actually owns).
/// </summary>
public static class JourneyEndpoints
{
    public static void MapJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/journeys")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapPost("/", async (CreateJourneyRequest request, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, request.ProjectId))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Journey name is required.");
            }

            var journey = await journeys.CreateJourneyAsync(request.ProjectId, request.Name);
            return Results.Created($"/api/journeys/{journey.Id}", new { journey.Id, journey.Name, journey.ProjectId, journey.CreatedAt });
        });

        group.MapGet("/", async (Guid projectId, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var list = await journeys.ListJourneysAsync(projectId);
            return Results.Ok(list.Select(j => new { j.Id, j.Name, j.ProjectId, j.CreatedAt }));
        });

        group.MapGet("/{journeyId:guid}", async (Guid journeyId, Guid projectId, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var journey = await journeys.GetJourneyAsync(projectId, journeyId);
            return journey is null ? Results.NotFound() : Results.Ok(new { journey.Id, journey.Name, journey.ProjectId, journey.CreatedAt });
        });

        group.MapGet("/{journeyId:guid}/versions", async (Guid journeyId, Guid projectId, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId) || !await journeys.ProjectOwnsJourneyAsync(projectId, journeyId))
            {
                return Results.Forbid();
            }

            var history = await journeys.ListVersionHistoryAsync(journeyId);
            return Results.Ok(history.Select(v => new { v.Version, v.CreatedByDisplayName, v.CreatedAt }));
        });

        group.MapGet("/{journeyId:guid}/versions/{version:int}", async (Guid journeyId, int version, Guid projectId, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId) || !await journeys.ProjectOwnsJourneyAsync(projectId, journeyId))
            {
                return Results.Forbid();
            }

            var journeyVersion = await journeys.GetVersionAsync(journeyId, version);
            return journeyVersion is null
                ? Results.NotFound()
                : Results.Ok(new { journeyVersion.Version, journeyVersion.YamlContent, journeyVersion.CreatedByDisplayName, journeyVersion.CreatedAt });
        });

        group.MapPost("/{journeyId:guid}/versions", async (Guid journeyId, Guid projectId, CreateJourneyVersionRequest request, JourneyService journeys, IProjectRepository projects, CurrentOrganizationAccessor currentOrg, ClaimsPrincipal user) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId) || !await journeys.ProjectOwnsJourneyAsync(projectId, journeyId))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.YamlContent))
            {
                return Results.BadRequest("Journey version content is required.");
            }

            var userId = user.FindFirstValue("user_id") ?? throw new InvalidOperationException("Authenticated request is missing a user_id claim.");
            var displayName = user.FindFirstValue("user_display_name") ?? userId;

            var version = await journeys.CreateVersionAsync(journeyId, request.YamlContent, userId, displayName);
            return Results.Created($"/api/journeys/{journeyId}/versions/{version.Version}", new { version.Version, version.CreatedByDisplayName, version.CreatedAt });
        });
    }
}

public sealed record CreateJourneyRequest(Guid ProjectId, string Name);
public sealed record CreateJourneyVersionRequest(string YamlContent);
