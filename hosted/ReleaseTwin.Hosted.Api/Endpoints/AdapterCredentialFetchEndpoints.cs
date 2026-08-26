using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-adapter-credentials: the CLI-facing fetch. Scoped entirely by the authenticated token's
/// project_id claim — there is no separate resource ID a wrong-project token could probe (unlike
/// hosted-journeys' journey ID), so cross-project leakage is impossible by the endpoint's shape, not
/// by an additional runtime check.
/// </summary>
public static class AdapterCredentialFetchEndpoints
{
    public static void MapAdapterCredentialFetchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cli/adapter-credentials")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        group.MapGet("/{adapter}", async (string adapter, AdapterCredentialService credentials, ClaimsPrincipal user) =>
        {
            var projectId = GetProjectId(user);
            var fields = await credentials.GetDecryptedFieldsAsync(projectId, adapter);
            if (fields is null)
            {
                // Distinct from a 401 (bad/missing token): a valid token for a project with nothing
                // configured for this adapter, per adapter-credentials' own requirement.
                return Results.NotFound();
            }

            return Results.Ok(new AdapterCredentialResponse { Adapter = adapter, Fields = fields });
        });
    }

    private static Guid GetProjectId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim) ?? throw new InvalidOperationException("Authenticated request is missing a project_id claim."));
}
