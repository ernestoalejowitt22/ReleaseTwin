using System.Net.Http.Json;
using System.Text.Json;

namespace ReleaseTwin.Cli.Upload;

/// <summary>How the upload credential was obtained, recorded in the run summary (<c>upload.mode</c>).</summary>
public static class UploadCredentialMode
{
    public const string None = "none";
    public const string Token = "token";
    public const string Oidc = "oidc";
    public const string OidcExchangeFailed = "oidc-exchange-failed";
}

/// <summary>The outcome of credential resolution. <see cref="Token"/> is null unless <see cref="Mode"/> is <c>token</c> or <c>oidc</c>.</summary>
public sealed record UploadCredentialResolution(string Mode, string? Token, string ApiUrl, string? FailureReason)
{
    public bool HasCredential => Token is { Length: > 0 };
    public bool Failed => Mode == UploadCredentialMode.OidcExchangeFailed;
}

/// <summary>
/// github-oidc-upload (design D5 in releasetwin-platform's github-oidc-ingest): one resolver for
/// every hosted upload path — <c>run</c> and <c>upload-junit</c> — in this order:
///
///   1. <c>RELEASETWIN_API_TOKEN</c> set → use it. Today's behaviour, wins over everything.
///   2. Else, a project is named (<c>RELEASETWIN_PROJECT_ID</c>) and the job carries GitHub's OIDC
///      request pair (<c>ACTIONS_ID_TOKEN_REQUEST_URL</c> / <c>_TOKEN</c>) → ask GitHub for a token
///      for this API's audience, exchange it at <c>POST /api/cli/auth/github</c>, use the short-lived
///      credential. The credential lives in memory for this process only.
///   3. Else → no credential, no error (the upload is optional).
///
/// Step 2 fails loudly: naming a project is declared intent, so a missing permission or a refused
/// exchange is reported with the exact fix rather than silently skipped.
/// </summary>
public static class UploadCredentialResolver
{
    public const string DefaultApiUrl = "https://api.releasetwin.com";
    public const string DefaultAudience = "api.releasetwin.com";
    public const string ExchangePath = "/api/cli/auth/github";

    /// <summary>The pre-OIDC placeholder base URL used only when a stored token is set without a URL — kept so that path is byte-for-byte unchanged.</summary>
    public const string LegacyTokenDefaultApiUrl = "https://api.releasetwin.example";

    public static async Task<UploadCredentialResolution> ResolveAsync(
        Func<string, string?> get,
        HttpMessageHandler? handlerForTesting = null,
        CancellationToken cancellationToken = default)
    {
        var configuredUrl = get("RELEASETWIN_API_URL") is { Length: > 0 } url ? url.TrimEnd('/') : null;

        var storedToken = get("RELEASETWIN_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(storedToken))
        {
            return new UploadCredentialResolution(UploadCredentialMode.Token, storedToken, configuredUrl ?? LegacyTokenDefaultApiUrl, null);
        }

        var projectId = get("RELEASETWIN_PROJECT_ID");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return new UploadCredentialResolution(UploadCredentialMode.None, null, configuredUrl ?? DefaultApiUrl, null);
        }

        var apiUrl = configuredUrl ?? DefaultApiUrl;
        if (!Guid.TryParse(projectId.Trim(), out var parsedProjectId))
        {
            return Failed(apiUrl, $"RELEASETWIN_PROJECT_ID is not a project id: '{projectId.Trim()}'. Copy it from the project's URL on the dashboard.");
        }

        var requestUrl = get("ACTIONS_ID_TOKEN_REQUEST_URL");
        var requestToken = get("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        if (string.IsNullOrWhiteSpace(requestUrl) || string.IsNullOrWhiteSpace(requestToken))
        {
            return Failed(apiUrl,
                "RELEASETWIN_PROJECT_ID is set but this job has no GitHub OIDC token to exchange. " +
                "Add `permissions: id-token: write` to the job (GitHub Actions), or set RELEASETWIN_API_TOKEN for a CI that has no OIDC.");
        }

        var audience = get("RELEASETWIN_OIDC_AUDIENCE") is { Length: > 0 } aud ? aud : DefaultAudience;

        using var http = new HttpClient(handlerForTesting ?? new HttpClientHandler(), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(30) };

        string gitHubToken;
        try
        {
            var separator = requestUrl.Contains('?') ? "&" : "?";
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{requestUrl}{separator}audience={Uri.EscapeDataString(audience)}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", requestToken);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(apiUrl, $"GitHub refused to issue an OIDC token (HTTP {(int)response.StatusCode}). Check that the job has `permissions: id-token: write`.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            gitHubToken = document.RootElement.TryGetProperty("value", out var value) ? value.GetString() ?? "" : "";
            if (gitHubToken.Length == 0)
            {
                return Failed(apiUrl, "GitHub's OIDC response carried no token value.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Failed(apiUrl, $"Could not obtain a GitHub OIDC token: {ex.Message}");
        }

        try
        {
            using var response = await http.PostAsJsonAsync($"{apiUrl}{ExchangePath}", new { token = gitHubToken, projectId = parsedProjectId }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var credential = document.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
                return string.IsNullOrEmpty(credential)
                    ? Failed(apiUrl, "ReleaseTwin's exchange response carried no credential.")
                    : new UploadCredentialResolution(UploadCredentialMode.Oidc, credential, apiUrl, null);
            }

            return Failed(apiUrl, (int)response.StatusCode switch
            {
                401 => "ReleaseTwin rejected the GitHub OIDC token. The job's token must be requested for audience " +
                       $"'{audience}' — this CLI does that itself, so a 401 usually means RELEASETWIN_API_URL points at an API that is not ReleaseTwin, or the token expired in transit.",
                404 => $"Project {parsedProjectId} is not bound to this repository. On the project's Settings page, set its repository to this GitHub repository, then re-run.",
                409 => "This GitHub OIDC token was already exchanged once. Each job run gets a fresh token; re-run the job.",
                429 => "ReleaseTwin rate-limited the exchange from this runner address. Retry shortly.",
                var status => $"ReleaseTwin's exchange endpoint returned HTTP {status}.",
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Failed(apiUrl, $"Could not reach the ReleaseTwin API at {apiUrl}: {ex.Message}");
        }
    }

    private static UploadCredentialResolution Failed(string apiUrl, string reason) =>
        new(UploadCredentialMode.OidcExchangeFailed, null, apiUrl, reason);
}
