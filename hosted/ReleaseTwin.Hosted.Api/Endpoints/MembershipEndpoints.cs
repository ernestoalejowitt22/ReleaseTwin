using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// org-membership: teams management — additional organizations, invitations, members, roles. All
/// ClerkJwt (web session) only; called from the Next.js BFF. Admin-gated operations go through
/// <see cref="IOrganizationAccessGuard.Require"/>; the route's organization id must match the caller's
/// active organization.
/// </summary>
public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/organizations")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        orgs.MapPost("/", async (CreateOrganizationRequest request, OrganizationMembersService members, IUserRepository users, ClaimsPrincipal principal) =>
        {
            var user = await CurrentUserAsync(users, principal);
            if (user is null)
            {
                return Results.Forbid();
            }

            var org = await members.CreateOrganizationAsync(user, request?.Name ?? "");
            return Results.Created($"/api/organizations/{org.Id}", new { org.Id, org.Name });
        });

        orgs.MapGet("/{organizationId:guid}/members", async (Guid organizationId, OrganizationMembersService members, CurrentOrganizationAccessor currentOrg) =>
        {
            if (currentOrg.OrganizationId != organizationId)
            {
                return Results.Forbid();
            }

            var list = await members.ListMembersAsync(organizationId);
            return Results.Ok(list.Select(m => new MemberView(m.UserId, m.Role.ToString(), m.DisplayName, m.Email, m.CreatedAt)));
        });

        orgs.MapPatch("/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, ChangeRoleRequest request, OrganizationMembersService members, CurrentOrganizationAccessor currentOrg) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            if (!Enum.TryParse<MembershipRole>(request?.Role, ignoreCase: true, out var role))
            {
                return Results.BadRequest(new { error = "invalid-role" });
            }

            await members.ChangeRoleAsync(organizationId, userId, role);
            return Results.NoContent();
        });

        orgs.MapDelete("/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, OrganizationMembersService members, CurrentOrganizationAccessor currentOrg) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            await members.RemoveMemberAsync(organizationId, userId);
            return Results.NoContent();
        });

        orgs.MapPost("/{organizationId:guid}/invitations", async (Guid organizationId, CreateInvitationRequest request, OrganizationMembersService members, IOrganizationRepository organizations, CurrentOrganizationAccessor currentOrg, ClaimsPrincipal principal, IConfiguration config) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            if (!Enum.TryParse<MembershipRole>(request?.Role, ignoreCase: true, out var role))
            {
                role = MembershipRole.Member;
            }

            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                return Results.BadRequest(new { error = "email-required" });
            }

            var invitedBy = Guid.TryParse(principal.FindFirstValue("user_id"), out var uid) ? uid : Guid.Empty;
            var invitation = await members.InviteAsync(organizationId, invitedBy, request.Email, role);

            var acceptUrl = AcceptUrl(config, invitation.Token);
            var org = await organizations.GetAsync(organizationId);
            await members.SendInvitationEmailAsync(invitation, org?.Name ?? "your team", acceptUrl);

            return Results.Ok(new InvitationView(invitation.Token, invitation.Email, invitation.Role.ToString(), invitation.State.ToString(), invitation.ExpiresAt, acceptUrl));
        });

        orgs.MapGet("/{organizationId:guid}/invitations", async (Guid organizationId, OrganizationMembersService members, CurrentOrganizationAccessor currentOrg, IConfiguration config) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            var list = await members.ListInvitationsAsync(organizationId);
            return Results.Ok(list.Select(i => new InvitationView(i.Token, i.Email, i.Role.ToString(), i.State.ToString(), i.ExpiresAt, AcceptUrl(config, i.Token))));
        });

        orgs.MapDelete("/{organizationId:guid}/invitations/{token}", async (Guid organizationId, string token, OrganizationMembersService members, CurrentOrganizationAccessor currentOrg) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            await members.RevokeInvitationAsync(organizationId, token);
            return Results.NoContent();
        });

        var invites = app.MapGroup("/api/invitations")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        invites.MapGet("/{token}", async (string token, IInvitationRepository invitations, IOrganizationRepository organizations) =>
        {
            var invitation = await invitations.GetByTokenAsync(token);
            if (invitation is null)
            {
                return Results.NotFound(new { error = "invitation-not-found" });
            }

            var org = await organizations.GetAsync(invitation.OrganizationId);
            return Results.Ok(new
            {
                organizationName = org?.Name,
                role = invitation.Role.ToString(),
                email = invitation.Email,
                acceptable = invitation.IsAcceptable(DateTimeOffset.UtcNow),
            });
        });

        invites.MapPost("/{token}/accept", async (string token, OrganizationMembersService members, IUserRepository users, ClaimsPrincipal principal) =>
        {
            var user = await CurrentUserAsync(users, principal);
            if (user is null)
            {
                return Results.Forbid();
            }

            try
            {
                var result = await members.AcceptAsync(user, token);
                return Results.Ok(new { organizationId = result.OrganizationId, role = result.Role.ToString() });
            }
            catch (InvitationInvalidException ex)
            {
                return Results.Json(new { error = "invitation-invalid", detail = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
        });
    }

    private static void RequireActive(CurrentOrganizationAccessor currentOrg, Guid organizationId, OrgCapability capability)
    {
        var active = currentOrg.Require(capability);
        if (active != organizationId)
        {
            throw new ForbiddenException("That organization is not your active organization.");
        }
    }

    private static async Task<AppUser?> CurrentUserAsync(IUserRepository users, ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        return sub is null ? null : await users.GetByClerkUserIdAsync(sub);
    }

    private static string AcceptUrl(IConfiguration config, string token)
    {
        var baseUrl = config["Web:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl)
            ? $"/invitations/{token}"
            : $"{baseUrl.TrimEnd('/')}/invitations/{token}";
    }
}

public sealed record CreateOrganizationRequest(string? Name);
public sealed record CreateInvitationRequest(string? Email, string? Role);
public sealed record ChangeRoleRequest(string? Role);
public sealed record MemberView(Guid UserId, string Role, string? DisplayName, string? Email, DateTimeOffset JoinedAt);
public sealed record InvitationView(string Token, string Email, string Role, string State, DateTimeOffset ExpiresAt, string AcceptUrl);
