using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// evidence-capture / evidence-store: the CLI reads a project's evidence capture default + retention
/// window (token-scoped, like adapter-credentials); the dashboard reads and sets them (web-session,
/// org-scoped, Paid tier to enable).
/// </summary>
public static class EvidenceConfigEndpoints
{
    public static void MapEvidenceConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var cli = app.MapGroup("/api/cli/evidence-config")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        cli.MapGet("/", async (IProjectRepository projects, ClaimsPrincipal user) =>
        {
            var orgId = Guid.Parse(user.FindFirstValue(ApiTokenDefaults.OrganizationIdClaim)!);
            var projectId = Guid.Parse(user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim)!);
            var project = await projects.GetAsync(orgId, projectId);
            if (project is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { captureDefault = project.EvidenceCaptureDefault, retentionDays = project.EvidenceRetentionDays });
        });

        var dash = app.MapGroup("/api/projects")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        dash.MapGet("/{projectId:guid}/evidence-config", async (Guid projectId, IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            var project = await projects.GetAsync(orgId.Value, projectId);
            if (project is null)
            {
                return Results.Forbid();
            }

            var organization = await organizations.GetAsync(orgId.Value);
            var ent = entitlements.For(organization);
            return Results.Ok(new
            {
                captureDefault = project.EvidenceCaptureDefault,
                retentionDays = project.EvidenceRetentionDays,
                maxRetentionDays = ent.MaxEvidenceRetentionDays ?? Project.MaxEvidenceRetentionDays,
                available = ent.EvidenceViewer,
            });
        });

        dash.MapPut("/{projectId:guid}/evidence-config", async (
            Guid projectId, SetEvidenceConfigRequest request,
            IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || await projects.GetAsync(orgId.Value, projectId) is null)
            {
                return Results.Forbid();
            }

            var organization = await organizations.GetAsync(orgId.Value);
            var ent = entitlements.For(organization);
            if (!ent.EvidenceViewer)
            {
                return Results.Json(new { error = "entitlement-required", entitlement = "evidenceViewer" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var maxRetention = ent.MaxEvidenceRetentionDays ?? Project.MaxEvidenceRetentionDays;
            if (request.RetentionDays < 1 || request.RetentionDays > maxRetention)
            {
                return Results.BadRequest(new { error = $"retentionDays must be between 1 and {maxRetention} on the {organization!.PlanTier} tier" });
            }

            await projects.SetEvidenceConfigAsync(orgId.Value, projectId, request.CaptureDefault, request.RetentionDays);
            return Results.NoContent();
        });
    }
}

public sealed record SetEvidenceConfigRequest(bool CaptureDefault, int RetentionDays);
