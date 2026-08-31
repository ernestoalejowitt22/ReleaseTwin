using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
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

        group.MapGet("/", async (Guid? projectId, DashboardService dashboard, CurrentOrganizationAccessor currentOrg,
            ReleaseTwin.Hosted.Api.Flags.IFlagService flags, ILoggerFactory loggerFactory) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            // add-feature-flag-seam: end-to-end proof the flag seam is wired on the hosted surface.
            // Structured log only; gates nothing. Delete when a real flag replaces flag-seam-smoke.
            var smoke = await flags.GetBooleanAsync("flag-seam-smoke");
            loggerFactory.CreateLogger("ReleaseTwin.Hosted.Api.Flags").LogInformation(
                "flag_seam_smoke surface=hosted value={Value}", smoke);

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

            try
            {
                var project = await provisioning.CreateProjectAsync(orgId.Value, request.Name);
                return Results.Created($"/api/dashboard?projectId={project.Id}", new { project.Id, project.Name });
            }
            catch (ProjectLimitExceededException)
            {
                // plan-tier-gating design.md: distinct from a generic 400/500 so the frontend can
                // show the right message and upgrade prompt rather than a validation error.
                return Results.Json(new { error = "free-tier-project-limit" }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        // billing (design.md D2): the upgrade endpoint creates a Merchant-of-Record checkout session
        // and returns its URL. It does NOT change the tier — only the webhook does, once payment
        // clears. Enterprise stays operator-set and unreachable here.
        group.MapPost("/upgrade", async (UpgradeRequest? request, ReleaseTwin.Hosted.Api.Billing.IPolarClient polar, ReleaseTwin.Hosted.Api.Billing.PolarOptions polarOptions, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            if (!polarOptions.IsUpgradeEnabled)
            {
                return Results.Json(new { error = "billing-not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            // design.md D7: monthly is the default cadence (small mid-cycle proration).
            var cadence = Enum.TryParse<Data.Entities.BillingCadence>(request?.Cadence, ignoreCase: true, out var c)
                ? c
                : Data.Entities.BillingCadence.Monthly;

            try
            {
                var session = await polar.CreateCheckoutSessionAsync(orgId.Value, Data.Entities.PlanTier.Team, cadence);
                return Results.Ok(new { checkoutUrl = session.Url });
            }
            catch (ReleaseTwin.Hosted.Api.Billing.PolarException)
            {
                return Results.Json(new { error = "checkout-unavailable" }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // billing: redirect-to-portal. 400 when the org has never checked out (no customer id).
        group.MapPost("/billing-portal", async (IOrganizationRepository organizations, ReleaseTwin.Hosted.Api.Billing.IPolarClient polar, ReleaseTwin.Hosted.Api.Billing.PolarOptions polarOptions, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null)
            {
                return Results.Forbid();
            }

            var org = await organizations.GetAsync(orgId.Value);
            if (org?.PolarCustomerId is not { Length: > 0 } customerId)
            {
                return Results.Json(new { error = "no-billing-linkage" }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (!polarOptions.IsUpgradeEnabled)
            {
                return Results.Json(new { error = "billing-not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                var session = await polar.CreatePortalSessionAsync(customerId);
                return Results.Ok(new { portalUrl = session.Url });
            }
            catch (ReleaseTwin.Hosted.Api.Billing.PolarException)
            {
                return Results.Json(new { error = "portal-unavailable" }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapDelete("/projects/{projectId:guid}", async (Guid projectId, ProvisioningService provisioning, CurrentOrganizationAccessor currentOrg, IProjectRepository projects) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await projects.ExistsInOrganizationAsync(orgId.Value, projectId))
            {
                return Results.Forbid();
            }

            await provisioning.DeleteProjectAsync(orgId.Value, projectId);
            return Results.NoContent();
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

        // evidence-store / dashboard: the redacted evidence document for one report, org-scoped.
        group.MapGet("/reports/{reportId:guid}/evidence", async (
            Guid reportId, Guid projectId, IProjectRepository projects, IRunEvidenceRepository evidence,
            CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || await projects.GetAsync(orgId.Value, projectId) is null)
            {
                return Results.Forbid();
            }

            var stored = await evidence.GetByReportAsync(projectId, reportId);
            if (stored is null)
            {
                return Results.NotFound();
            }

            using var doc = JsonDocument.Parse(stored.DocumentJson);
            return Results.Ok(new
            {
                document = doc.RootElement.Clone(),
                stored.ScreenshotIds,
                stored.UploadedAt,
            });
        });

        group.MapGet("/evidence-screenshots/{screenshotId}", async (
            string screenshotId, Guid projectId, Guid reportId,
            IProjectRepository projects, IRunEvidenceRepository evidence, IEvidenceBlobStore blobs,
            CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || await projects.GetAsync(orgId.Value, projectId) is null)
            {
                return Results.Forbid();
            }

            // The screenshot must belong to an evidence document in this project.
            var stored = await evidence.GetByReportAsync(projectId, reportId);
            if (stored is null || !stored.ScreenshotIds.Contains(screenshotId))
            {
                return Results.NotFound();
            }

            var bytes = await blobs.GetAsync(screenshotId);
            return bytes is null ? Results.NotFound() : Results.File(bytes, "image/png");
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

public sealed record UpgradeRequest(string? Cadence);
