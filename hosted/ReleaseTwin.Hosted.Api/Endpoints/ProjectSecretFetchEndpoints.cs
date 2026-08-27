using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-project-secrets: the CLI-facing fetch. Scoped entirely by the authenticated token's
/// project_id claim — there is no separate resource ID a wrong-project token could probe (same
/// structurally-impossible-to-leak-cross-project shape as adapter-credentials' own fetch endpoint).
/// </summary>
public static class ProjectSecretFetchEndpoints
{
    public static void MapProjectSecretFetchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cli/project-secrets")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        group.MapGet("/", async (ProjectSecretService secrets, ClaimsPrincipal user) =>
        {
            var projectId = GetProjectId(user);
            var all = await secrets.GetAllDecryptedAsync(projectId);
            return Results.Ok(new ProjectSecretsResponse { Secrets = all });
        });
    }

    private static Guid GetProjectId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim) ?? throw new InvalidOperationException("Authenticated request is missing a project_id claim."));
}
