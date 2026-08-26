using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record GitHubAuthorizeResult(bool Configured, string? AuthorizeUrl);

public sealed record GitHubCallbackResult(Guid ProjectId, IReadOnlyList<string> Repositories);

/// <summary>
/// hosted-react-frontend: extracted from the old Connections/Start.cshtml.cs and Callback.cshtml.cs
/// so the "token never persisted" guarantee is directly unit-testable, same pattern as
/// DashboardService. project-connections spec: "The GitHub access token is never persisted" — the
/// token exchanged in <see cref="ExchangeCodeForRepositoriesAsync"/> lives only as a local variable
/// there, never returned, never assigned to a field.
/// </summary>
public sealed class GitHubConnectionFlowService
{
    private readonly IConnectionStateService _state;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubConnectionFlowService(IConnectionStateService state, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _state = state;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public GitHubAuthorizeResult BuildAuthorizeUrl(Guid projectId)
    {
        var clientId = _configuration["GitHubConnection:ClientId"];
        var redirectUri = _configuration["GitHubConnection:CallbackUrl"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return new GitHubAuthorizeResult(false, null);
        }

        var mintedState = _state.Mint(projectId);

        // github-oauth-private-repos: classic GitHub OAuth Apps have no scope that means "list
        // private repos without reading their content" — `repo` is the only scope covering private
        // repos at all, and it's broader than what this app actually uses. This app's own code
        // still never reads anything beyond the repo list itself (see project-connections spec's
        // "A broader OAuth grant is not exercised beyond listing repositories").
        var authorizeUrl = "https://github.com/login/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&scope=read%3Auser%20repo"
            + $"&state={Uri.EscapeDataString(mintedState)}";

        return new GitHubAuthorizeResult(true, authorizeUrl);
    }

    public async Task<GitHubCallbackResult?> ExchangeCodeForRepositoriesAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        var projectId = _state.Validate(state);
        if (projectId is null)
        {
            return null;
        }

        var clientId = _configuration["GitHubConnection:ClientId"];
        var clientSecret = _configuration["GitHubConnection:ClientSecret"];
        var redirectUri = _configuration["GitHubConnection:CallbackUrl"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        using var client = _httpClientFactory.CreateClient("GitHubConnection");
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReleaseTwin", "1.0"));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } },
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }),
        };

        // The access token exists only as this local variable — never assigned to a field, a cache,
        // session, or a log, and this method never returns it.
        using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<GitHubAccessTokenResponse>(cancellationToken: cancellationToken);
        var accessToken = tokenPayload?.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        using var reposRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/repos?sort=full_name");
        reposRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        reposRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var reposResponse = await client.SendAsync(reposRequest, cancellationToken);
        reposResponse.EnsureSuccessStatusCode();
        var repos = await reposResponse.Content.ReadFromJsonAsync<List<GitHubRepo>>(cancellationToken: cancellationToken) ?? new();

        return new GitHubCallbackResult(
            projectId.Value,
            repos.Select(r => r.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    private sealed class GitHubAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class GitHubRepo
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;
    }
}
