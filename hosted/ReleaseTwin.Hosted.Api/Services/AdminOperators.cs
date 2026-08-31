namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// plan-catalog-and-entitlements: the allowlist of Clerk user ids permitted to call the operator-only
/// admin endpoints (e.g. setting an organization to the Enterprise tier). Sourced from the
/// <c>Admin:OperatorUserIds</c> configuration value — comma-, space-, or newline-separated Clerk user
/// ids (the <c>sub</c> claim on the session JWT). Empty / unset means nobody is an operator, so the
/// admin surface is closed by default.
/// </summary>
public sealed class AdminOperators
{
    private readonly HashSet<string> _operatorUserIds;

    public AdminOperators(IConfiguration configuration)
        : this(configuration["Admin:OperatorUserIds"])
    {
    }

    internal AdminOperators(string? rawValue)
    {
        _operatorUserIds = (rawValue ?? string.Empty)
            .Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool Any => _operatorUserIds.Count > 0;

    public bool IsOperator(string? clerkUserId) =>
        !string.IsNullOrEmpty(clerkUserId) && _operatorUserIds.Contains(clerkUserId);
}
