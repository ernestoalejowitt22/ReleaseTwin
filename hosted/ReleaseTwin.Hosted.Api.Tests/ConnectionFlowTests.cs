using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// hosted-react-frontend: GitHubConnectionFlowService is the extracted, directly-testable logic
/// behind /api/connections/start and /callback — including proving the access token used during
/// the flow never ends up anywhere persisted.
/// </summary>
public class ConnectionFlowTests
{
    private static HostedDbContext NewDb() => new(
        new DbContextOptionsBuilder<HostedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration ConfiguredGitHub() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHubConnection:ClientId"] = "test-client-id",
            ["GitHubConnection:ClientSecret"] = "test-client-secret",
            ["GitHubConnection:CallbackUrl"] = "https://localhost/connect/github/callback",
        })
        .Build();

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        public string IssuedAccessToken { get; } = "gho_fake_token_" + Guid.NewGuid().ToString("N");
        public bool ReposCalledWithCorrectToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.ToString() == "https://github.com/login/oauth/access_token")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"access_token":"{{IssuedAccessToken}}","token_type":"bearer"}""", System.Text.Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Get && request.RequestUri!.ToString().StartsWith("https://api.github.com/user/repos"))
            {
                ReposCalledWithCorrectToken = request.Headers.Authorization?.Parameter == IssuedAccessToken;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"full_name":"acme/checkout-service"},{"full_name":"acme/billing"}]""", System.Text.Encoding.UTF8, "application/json"),
                });
            }

            throw new InvalidOperationException($"Unhandled fake GitHub request: {request.Method} {request.RequestUri}");
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static GitHubConnectionFlowService NewFlow(HttpMessageHandler handler, IConnectionStateService? stateService = null) =>
        new(stateService ?? new ConnectionStateService(new EphemeralDataProtectionProvider()), ConfiguredGitHub(), new FakeHttpClientFactory(handler));

    [Fact]
    public void BuildAuthorizeUrlReturnsUnconfiguredWithoutGitHubSettings()
    {
        var flow = new GitHubConnectionFlowService(
            new ConnectionStateService(new EphemeralDataProtectionProvider()),
            new ConfigurationBuilder().Build(),
            new FakeHttpClientFactory(new FakeGitHubHandler()));

        var result = flow.BuildAuthorizeUrl(Guid.NewGuid());

        Assert.False(result.Configured);
        Assert.Null(result.AuthorizeUrl);
    }

    [Fact]
    public void BuildAuthorizeUrlIncludesTheMintedStateAndProjectScope()
    {
        var flow = NewFlow(new FakeGitHubHandler());

        var result = flow.BuildAuthorizeUrl(Guid.NewGuid());

        Assert.True(result.Configured);
        Assert.StartsWith("https://github.com/login/oauth/authorize", result.AuthorizeUrl);
        Assert.Contains("state=", result.AuthorizeUrl);
        Assert.Contains("scope=read%3Auser", result.AuthorizeUrl);
    }

    [Fact]
    public async Task ExchangeListsRepositoriesUsingTheExchangedToken()
    {
        var stateService = new ConnectionStateService(new EphemeralDataProtectionProvider());
        var projectId = Guid.NewGuid();
        var state = stateService.Mint(projectId);
        var handler = new FakeGitHubHandler();
        var flow = NewFlow(handler, stateService);

        var result = await flow.ExchangeCodeForRepositoriesAsync("some-code", state);

        Assert.NotNull(result);
        Assert.Equal(projectId, result!.ProjectId);
        Assert.Equal(new[] { "acme/billing", "acme/checkout-service" }, result.Repositories);
        Assert.True(handler.ReposCalledWithCorrectToken);
    }

    // Scenario: The GitHub access token is never persisted
    [Fact]
    public async Task NoTokenShapedValueEverReachesTheDatabase()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var user = await provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");

        var stateService = new ConnectionStateService(new EphemeralDataProtectionProvider());
        var state = stateService.Mint(project.Id);
        var handler = new FakeGitHubHandler();
        var flow = NewFlow(handler, stateService);

        var callbackResult = await flow.ExchangeCodeForRepositoriesAsync("some-code", state);
        Assert.NotNull(callbackResult);

        // Only the already-chosen repo name crosses into ConnectionService.ConnectAsync — the token
        // from the exchange above was never passed to it and cannot be retrieved from the flow result.
        await new ConnectionService(db).ConnectAsync(project.Id, "github", callbackResult!.Repositories[0]);

        var connection = await db.Connections.SingleAsync(c => c.ProjectId == project.Id);
        Assert.DoesNotContain(handler.IssuedAccessToken, connection.ExternalRepo);

        var everyStoredString = string.Join('|', db.Connections.Select(c => c.Provider + c.ExternalRepo))
            + string.Join('|', db.Users.Select(u => u.ClerkUserId + u.DisplayName));
        Assert.DoesNotContain(handler.IssuedAccessToken, everyStoredString);
    }

    [Fact]
    public async Task InvalidStateReturnsNullInsteadOfCrashing()
    {
        var flow = NewFlow(new FakeGitHubHandler());

        var result = await flow.ExchangeCodeForRepositoriesAsync("some-code", "not-a-real-state");

        Assert.Null(result);
    }

    // Scenario: A project outside the signed-in organization cannot be connected
    [Fact]
    public async Task ConnectionForAnotherOrganizationsProjectIsRejected()
    {
        await using var db = NewDb();
        var provisioning = new ProvisioningService(db, new TokenService());
        var orgAUser = await provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectB = await provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");
        var connections = new ConnectionService(db);

        var belongsToOrgA = await connections.ProjectBelongsToOrganizationAsync(projectB.Id, orgAUser.OrganizationId);

        Assert.False(belongsToOrgA);
    }
}
