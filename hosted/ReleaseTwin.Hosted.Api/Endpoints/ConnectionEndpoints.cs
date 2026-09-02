using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// project-connections, converted for hosted-react-frontend: the browser's actual OAuth round trip
/// through GitHub happens via Next.js (the only public-facing app now, per the BFF design) — this
/// API is only ever called server-to-server by Next.js, authenticated, and never sees the browser
/// directly. The trust-sensitive work (token used once, held only in a local variable, never
/// persisted) lives in <see cref="GitHubConnectionFlowService"/>, unchanged from the original
/// Razor Pages implementation.
/// </summary>
public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapPost("/start", async (StartConnectionRequest request, ConnectionService connections, GitHubConnectionFlowService flow, CurrentOrganizationAccessor currentOrg, System.Security.Claims.ClaimsPrincipal principal) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || !await connections.ProjectBelongsToOrganizationAsync(request.ProjectId, orgId.Value))
            {
                return Results.Forbid();
            }

            // security-hardening-pre-pilot D6: bind the OAuth state to the initiating user.
            if (!Guid.TryParse(principal.FindFirst("user_id")?.Value, out var userId))
            {
                return Results.Forbid();
            }

            return Results.Ok(flow.BuildAuthorizeUrl(request.ProjectId, userId));
        });

        group.MapPost("/callback", async (ConnectionCallbackRequest request, GitHubConnectionFlowService flow, System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirst("user_id")?.Value, out var userId))
            {
                return Results.BadRequest(new { error = "That connection attempt expired, was invalid, or GitHub connections are not configured — try again." });
            }

            var result = await flow.ExchangeCodeForRepositoriesAsync(request.Code, request.State, userId, cancellationToken);
            if (result is null)
            {
                return Results.BadRequest(new { error = "That connection attempt expired, was invalid, or GitHub connections are not configured — try again." });
            }

            return Results.Ok(result);
        });

        group.MapPost("/confirm", async (ConfirmConnectionRequest request, ConnectionService connections, CurrentOrganizationAccessor currentOrg) =>
        {
            var orgId = currentOrg.OrganizationId;
            if (orgId is null || string.IsNullOrWhiteSpace(request.ExternalRepo)
                || !await connections.ProjectBelongsToOrganizationAsync(request.ProjectId, orgId.Value))
            {
                return Results.Forbid();
            }

            await connections.ConnectAsync(request.ProjectId, "github", request.ExternalRepo);
            return Results.NoContent();
        });
    }
}

public sealed record StartConnectionRequest(Guid ProjectId);
public sealed record ConnectionCallbackRequest(string Code, string State);
public sealed record ConfirmConnectionRequest(Guid ProjectId, string ExternalRepo);
