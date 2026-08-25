using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-react-frontend: JSON equivalent of the old Dashboard.cshtml.cs Razor Page — same
/// org-scoping discipline (dashboard spec: "A customer sees only their own organization's data"),
/// called only from the Next.js BFF, never directly from a browser.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("/", async (Guid? projectId, DashboardService dashboard, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            return Results.Ok(await dashboard.GetDashboardViewAsync(orgId.Value, projectId));
        });

        group.MapPost("/projects", async (CreateProjectRequest request, ProvisioningService provisioning, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Project name is required.");
            }

            var project = await provisioning.CreateProjectAsync(orgId.Value, request.Name);
            return Results.Created($"/api/dashboard?projectId={project.Id}", new { project.Id, project.Name });
        });

        group.MapPost("/projects/{projectId:guid}/tokens", async (Guid projectId, ProvisioningService provisioning, CurrentOrganizationAccessor currentOrg, IProjectRepository projects) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            var (_, raw) = await provisioning.IssueTokenAsync(projectId, orgId.Value);
            return Results.Ok(new { token = raw });
        });

        group.MapDelete("/projects/{projectId:guid}/tokens/{tokenId:guid}", async (Guid projectId, Guid tokenId, ProvisioningService provisioning, CurrentOrganizationAccessor currentOrg, IProjectRepository projects) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            await provisioning.RevokeTokenAsync(tokenId);
            return Results.NoContent();
        });

        group.MapDelete("/projects/{projectId:guid}/connection", async (Guid projectId, ConnectionService connections, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await connections.ProjectBelongsToOrganizationAsync(projectId, orgId.Value))
            {
                return Results.Forbid();
            }

            await connections.DisconnectAsync(projectId);
            return Results.NoContent();
        });
    }
}

public sealed record CreateProjectRequest(string Name);
