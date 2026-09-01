using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Services;
using ReleaseTwin.Hosted.Api.Services.DataExport;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// data-export: one admin-gated endpoint that produces a full archive of the organization's run
/// history + evidence. When an archive store is configured it returns a short-lived download URL the
/// browser follows directly (design D2); otherwise (dev / tests) it streams the ZIP in the response.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/export", async (
            ExportArchiveBuilder builder, IExportArchiveStore store, CurrentOrganizationAccessor currentOrg,
            CancellationToken cancellationToken) =>
        {
            var organizationId = currentOrg.Require(OrgCapability.ExportData);

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var displayName = $"releasetwin-export-{organizationId}-{timestamp}.zip";

            var zip = await builder.BuildAsync(organizationId, cancellationToken);

            var stored = await store.StoreAsync(zip, $"{organizationId}/{displayName}", cancellationToken);
            return stored is not null
                ? Results.Ok(new { downloadUrl = stored.DownloadUrl, expiresAt = stored.ExpiresAt })
                : Results.File(zip, "application/zip", displayName);
        })
        .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));
    }
}
