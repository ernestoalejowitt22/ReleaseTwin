namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// hosted-adapter-credentials design.md: each adapter's required field names, declared once here so
/// a dashboard submission can be validated for completeness without the hosted API knowing anything
/// else about what an adapter does. The CLI side declares its own independent copy of these same
/// names (this hosted API and the CLI are separate solutions/deployments that deliberately don't
/// share a compiled type — same convention as every other cross-boundary contract in this codebase).
/// </summary>
internal static class AdapterCredentialFieldManifests
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByAdapter = new Dictionary<string, IReadOnlyList<string>>
    {
        ["azure-devops"] = new[] { "org", "project", "pat", "areaPath", "variableGroupId" },
        ["launchdarkly"] = new[] { "apiToken", "projectKey", "environmentKey" },
    };

    public static bool IsKnownAdapter(string adapter) => ByAdapter.ContainsKey(adapter);

    public static IReadOnlyList<string>? MissingFields(string adapter, IReadOnlyDictionary<string, string> submitted)
    {
        if (!ByAdapter.TryGetValue(adapter, out var required))
        {
            return null;
        }

        return required.Where(field => !submitted.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value)).ToList();
    }
}
