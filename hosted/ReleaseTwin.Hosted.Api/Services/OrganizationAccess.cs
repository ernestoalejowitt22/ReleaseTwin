using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// org-membership design D4: the organization-level operations a <see cref="MembershipRole"/> may or may
/// not perform. One small enum consulted through <see cref="IOrganizationAccessGuard"/>, rather than
/// scattered role comparisons across endpoints.
/// </summary>
public enum OrgCapability
{
    ManageBilling,
    ManageTokens,
    ManageMembers,
    ManageNotifications,
    UseProjects,
    ViewEvidence,
}

/// <summary>org-membership: static role → capability table (design D4/D9). Admin can do everything;
/// Member triggers project work and views evidence; Viewer views evidence only.</summary>
public static class OrgCapabilities
{
    public static bool Allows(MembershipRole role, OrgCapability capability) => role switch
    {
        MembershipRole.Admin => true,
        MembershipRole.Member => capability is OrgCapability.UseProjects or OrgCapability.ViewEvidence,
        MembershipRole.Viewer => capability is OrgCapability.ViewEvidence,
        _ => false,
    };
}

/// <summary>
/// org-membership: thrown when the caller has no membership in the active organization, or their
/// membership role does not permit the attempted capability. Endpoints map it to a 403 with error
/// code <c>forbidden</c>.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>
/// org-membership design D4: the single authorizer every organization-scoped ClerkJwt endpoint calls.
/// The active organization and the caller's role in it are resolved once during token validation and
/// stamped as claims (<c>org_id</c>, <c>org_role</c>); this guard reads them back.
/// </summary>
public interface IOrganizationAccessGuard
{
    /// <summary>The active organization for this request, or null if the caller is not a member of any.</summary>
    Guid? OrganizationId { get; }

    /// <summary>The caller's role in the active organization, or null if there is no active organization.</summary>
    MembershipRole? Role { get; }

    /// <summary>
    /// Ensures there is an active organization and the caller's role permits <paramref name="capability"/>.
    /// Returns the active organization id. Throws <see cref="ForbiddenException"/> otherwise.
    /// </summary>
    Guid Require(OrgCapability capability);
}
