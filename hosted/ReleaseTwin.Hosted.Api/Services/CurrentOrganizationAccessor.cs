using System.Security.Claims;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// dashboard spec: "A customer sees only their own organization's data" — every dashboard query
/// goes through this to get the org id from the authenticated web session's claims, never from a
/// client-supplied value (a query string org id, a form field, etc.), so there is no way to ask for
/// someone else's data by changing a parameter.
///
/// org-membership: also the request-scoped <see cref="IOrganizationAccessGuard"/>. The active
/// organization and the caller's role in it are resolved once during token validation (from the
/// caller's memberships and an optional <c>X-Org-Id</c> header) and stamped as the <c>org_id</c> /
/// <c>org_role</c> claims this reads back.
/// </summary>
public sealed class CurrentOrganizationAccessor : IOrganizationAccessGuard
{
    /// <summary>org-membership: the BFF sends the viewer's chosen active organization here. Validated
    /// against the caller's memberships during token validation — an unknown or non-member value is
    /// ignored in favour of the caller's default organization.</summary>
    public const string ActiveOrgHeader = "X-Org-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentOrganizationAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OrganizationId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("org_id");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public MembershipRole? Role
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("org_role");
            return Enum.TryParse<MembershipRole>(claim, ignoreCase: true, out var role) ? role : null;
        }
    }

    public Guid Require(OrgCapability capability)
    {
        if (OrganizationId is not { } organizationId)
        {
            throw new ForbiddenException("No active organization for this request.");
        }

        if (Role is not { } role || !OrgCapabilities.Allows(role, capability))
        {
            throw new ForbiddenException($"Your role does not permit {capability}.");
        }

        return organizationId;
    }
}
