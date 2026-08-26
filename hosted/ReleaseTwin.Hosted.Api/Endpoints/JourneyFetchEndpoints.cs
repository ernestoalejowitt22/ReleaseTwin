using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// hosted-journeys: the first hosted capability the CLI fetches something to *execute* rather than
/// only reports to (distinct in kind from ingest-api). `version` is a required route segment — there
/// is no route that resolves to "whatever is currently latest" (hosted-journeys spec: "Running a
/// hosted journey SHALL require specifying which version to run").
/// </summary>
public static class JourneyFetchEndpoints
{
    public static void MapJourneyFetchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cli/journeys")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme));

        group.MapGet("/{journeyId:guid}/versions/{version:int}", async (Guid journeyId, int version, JourneyService journeys, ClaimsPrincipal user) =>
        {
            var projectId = GetProjectId(user);

            if (!await journeys.ProjectOwnsJourneyAsync(projectId, journeyId))
            {
                // Same response whether the journey doesn't exist or belongs to a different
                // project's token — a wrong-project fetch must not reveal that the journey exists.
                return Results.NotFound();
            }

            var journeyVersion = await journeys.GetVersionAsync(journeyId, version);
            if (journeyVersion is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new JourneyVersionResponse
            {
                JourneyId = journeyVersion.JourneyId,
                Version = journeyVersion.Version,
                YamlContent = journeyVersion.YamlContent,
            });
        });
    }

    private static Guid GetProjectId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ApiTokenDefaults.ProjectIdClaim) ?? throw new InvalidOperationException("Authenticated request is missing a project_id claim."));
}
