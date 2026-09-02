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

        // org-membership: the organizations the current user belongs to — powers the header org
        // switcher and tells a page whether the caller is an admin of the active org.
        app.MapGet("/api/me/organizations", async (MembershipService membershipService, IUserRepository users,
            IOrganizationRepository organizations, CurrentOrganizationAccessor currentOrg, ClaimsPrincipal principal) =>
        {
            var user = await CurrentUserAsync(users, principal);
            if (user is null)
            {
                return Results.Ok(Array.Empty<object>());
            }

            var memberships = await membershipService.GetMembershipsAsync(user);
            var views = new List<MyOrganizationView>(memberships.Count);
            foreach (var m in memberships)
            {
                var org = await organizations.GetAsync(m.OrganizationId);
                views.Add(new MyOrganizationView(
                    m.OrganizationId,
                    org?.Name ?? "(unknown)",
                    m.Role.ToString(),
                    Active: currentOrg.OrganizationId == m.OrganizationId));
            }

            return Results.Ok(views.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase));
        }).RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

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

        // company-and-domain-launch (4.12): re-send the invite email for a still-pending invitation.
        orgs.MapPost("/{organizationId:guid}/invitations/{token}/resend", async (Guid organizationId, string token, OrganizationMembersService members, IOrganizationRepository organizations, CurrentOrganizationAccessor currentOrg, IConfiguration config) =>
        {
            RequireActive(currentOrg, organizationId, OrgCapability.ManageMembers);
            var acceptUrl = AcceptUrl(config, token);
            var org = await organizations.GetAsync(organizationId);
            var invitation = await members.ResendInvitationEmailAsync(organizationId, token, org?.Name ?? "your team", acceptUrl);
            return invitation is null
                ? Results.NotFound(new { error = "invitation-not-found" })
                : Results.Ok(new InvitationView(invitation.Token, invitation.Email, invitation.Role.ToString(), invitation.State.ToString(), invitation.ExpiresAt, acceptUrl));
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
            // security-hardening-pre-pilot D2: the preview is reachable by any authenticated user who
            // holds the link — it must not disclose the invited email address. Acceptance itself
            // checks the caller's verified email server-side (OrganizationMembersService.AcceptAsync).
            return Results.Ok(new
            {
                organizationName = org?.Name,
                role = invitation.Role.ToString(),
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
public sealed record MyOrganizationView(Guid Id, string Name, string Role, bool Active);
public sealed record InvitationView(string Token, string Email, string Role, string State, DateTimeOffset ExpiresAt, string AcceptUrl);
