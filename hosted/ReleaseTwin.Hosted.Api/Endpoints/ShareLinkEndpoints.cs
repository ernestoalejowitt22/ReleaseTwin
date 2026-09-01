using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// evidence-sharing (design D7): the authenticated management surface for per-run share links, plus
/// the single UNAUTHENTICATED route that resolves a link token to the redacted evidence view. The
/// unauthenticated route sits under its own path (not the dashboard tree) and returns only a
/// <see cref="SharedEvidenceView"/>.
/// </summary>
public static class ShareLinkEndpoints
{
    public static void MapShareLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var manage = app.MapGroup("/api/reports/{reportId:guid}/share-links")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        manage.MapPost("/", async (Guid reportId, Guid projectId, EvidenceSharingService sharing, IProjectRepository projects,
            IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg,
            ClaimsPrincipal principal, IConfiguration config) =>
        {
            var gate = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (gate is not null)
            {
                return gate;
            }

            var createdBy = Guid.TryParse(principal.FindFirstValue("user_id"), out var uid) ? uid : Guid.Empty;
            try
            {
                var (link, token) = await sharing.CreateAsync(currentOrg.OrganizationId!.Value, projectId, reportId, createdBy);
                return Results.Ok(new
                {
                    id = link.Id,
                    token,
                    url = ShareUrl(config, token),
                    expiresAt = link.ExpiresAt,
                });
            }
            catch (ShareTargetNotFoundException)
            {
                return Results.NotFound(new { error = "report-not-found" });
            }
        });

        manage.MapGet("/", async (Guid reportId, Guid projectId, EvidenceSharingService sharing, IProjectRepository projects,
            IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var gate = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            return gate ?? Results.Ok(await sharing.ListAsync(reportId));
        });

        manage.MapDelete("/{linkId:guid}", async (Guid reportId, Guid linkId, Guid projectId, EvidenceSharingService sharing,
            IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var gate = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (gate is not null)
            {
                return gate;
            }

            await sharing.RevokeAsync(reportId, linkId);
            return Results.NoContent();
        });

        // --- unauthenticated ---
        // security-hardening-pre-pilot D7: per-client-address ceiling — sized so a viewer loading a
        // shared page and all its screenshots is never throttled, but token guessing is shed.
        app.MapGet("/api/shared-runs/{token}", async (string token, EvidenceSharingService sharing) =>
        {
            try
            {
                return Results.Ok(await sharing.ResolveAsync(token));
            }
            catch (ShareLinkUnavailableException)
            {
                return Results.NotFound(new { error = "share-link-unavailable" });
            }
            catch (ShareEntitlementRevokedException)
            {
                return Results.Json(new { error = "share-link-unavailable" }, statusCode: StatusCodes.Status403Forbidden);
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimiting.ShareLinkPolicy);

        app.MapGet("/api/shared-runs/{token}/screenshots/{screenshotId}", async (string token, string screenshotId,
            EvidenceSharingService sharing, IEvidenceBlobStore blobs) =>
        {
            try
            {
                var (bytes, contentType) = await sharing.ResolveScreenshotAsync(token, screenshotId, blobs);
                return bytes is null ? Results.NotFound() : Results.File(bytes, contentType);
            }
            catch (ShareLinkUnavailableException)
            {
                return Results.NotFound();
            }
            catch (ShareEntitlementRevokedException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimiting.ShareLinkPolicy);
    }

    private static async Task<IResult?> GateAsync(Guid projectId, CurrentOrganizationAccessor currentOrg,
        IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements)
    {
        var orgId = currentOrg.Require(OrgCapability.ManageSharing);
        if (await projects.GetAsync(orgId, projectId) is null)
        {
            return Results.Forbid();
        }

        var organization = await organizations.GetAsync(orgId);
        return entitlements.For(organization).EvidenceSharing
            ? null
            : Results.Json(new { error = "entitlement-required", entitlement = "evidenceSharing" }, statusCode: StatusCodes.Status403Forbidden);
    }

    private static string ShareUrl(IConfiguration config, string token)
    {
        var baseUrl = config["Web:BaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? $"/share/{token}" : $"{baseUrl}/share/{token}";
    }
}
