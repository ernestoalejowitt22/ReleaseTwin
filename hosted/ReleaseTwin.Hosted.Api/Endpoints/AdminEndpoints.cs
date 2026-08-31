using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// plan-catalog-and-entitlements: operator-only endpoints. Authenticated as a normal Clerk web
/// session, then additionally gated on the caller's Clerk user id being in the
/// <c>Admin:OperatorUserIds</c> allowlist (<see cref="AdminOperators"/>). This is the code path for
/// the one tier transition that is deliberately not self-serve — moving an organization to
/// <see cref="PlanTier.Enterprise"/> for a sales deal — so it never requires poking DynamoDB by hand.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapPut("/organizations/{organizationId:guid}/tier", async (
            Guid organizationId, SetTierRequest request,
            ClaimsPrincipal user, AdminOperators operators,
            ProvisioningService provisioning, IOrganizationRepository organizations) =>
        {
            var callerClerkUserId = user.FindFirstValue("sub");
            if (!operators.IsOperator(callerClerkUserId))
            {
                // Not an operator — indistinguishable from the route not existing.
                return Results.NotFound();
            }

            if (!Enum.TryParse<PlanTier>(request.Tier, ignoreCase: true, out var tier))
            {
                return Results.BadRequest(new { error = $"unknown tier '{request.Tier}'", allowed = Enum.GetNames<PlanTier>() });
            }

            if (await organizations.GetAsync(organizationId) is null)
            {
                return Results.NotFound(new { error = "organization not found" });
            }

            await provisioning.SetTierAsync(organizationId, tier);
            return Results.Ok(new { organizationId, tier = tier.ToString() });
        });
    }
}

public sealed record SetTierRequest(string Tier);
