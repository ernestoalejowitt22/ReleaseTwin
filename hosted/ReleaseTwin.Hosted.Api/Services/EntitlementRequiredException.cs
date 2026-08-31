namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// plan-catalog-and-entitlements: thrown when an operation requires an entitlement the caller's
/// organization tier does not include (e.g. storing a project secret without <c>projectSecrets</c>).
/// Endpoints map it to a 403 with a distinct error code so the frontend can show the right upgrade
/// prompt rather than a generic error.
/// </summary>
public sealed class EntitlementRequiredException : Exception
{
    /// <summary>The catalog entitlement key that was missing, e.g. <c>projectSecrets</c>.</summary>
    public string Entitlement { get; }

    public EntitlementRequiredException(string entitlement, string message) : base(message)
    {
        Entitlement = entitlement;
    }
}
