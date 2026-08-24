namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>
/// Connection details for one Azure DevOps organization/project. The personal access token is
/// supplied by the caller (environment variable, secret store, etc.) — this type never resolves
/// it itself, and no adapter file may contain a token literal (see adapter-sdk's external-credentials requirement).
/// </summary>
public sealed record AzureDevOpsOptions(string Organization, string Project, string PersonalAccessToken);
