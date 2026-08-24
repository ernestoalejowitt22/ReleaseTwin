using System.Security.Claims;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// dashboard spec: "A customer sees only their own organization's data" — every dashboard query
/// goes through this to get the org id from the authenticated web session's claims, never from a
/// client-supplied value (a query string org id, a form field, etc.), so there is no way to ask for
/// someone else's data by changing a parameter.
/// </summary>
public sealed class CurrentOrganizationAccessor
{
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
}
