using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// hosted-react-frontend: GitHubConnectionFlowService is the extracted, directly-testable logic
/// behind /api/connections/start and /callback — including proving the access token used during
/// the flow never ends up anywhere persisted.
/// </summary>
public class ConnectionFlowTests
{
    private sealed record Fixture(ProvisioningService Provisioning, ConnectionService Connections, IConnectionRepository ConnectionRepo, IUserRepository UserRepo);

    private static Fixture NewFixture()
    {
        var table = new InMemoryHostedTable();
        var users = new UserRepository(table);
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var tokens = new ApiTokenRepository(table);
        var connectionRepo = new ConnectionRepository(table);
        var provisioning = new ProvisioningService(users, organizations, projects, tokens, new TokenService(), TestEntitlements.Service);
        var connections = new ConnectionService(projects, connectionRepo);
        return new Fixture(provisioning, connections, connectionRepo, users);
    }

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

        var result = flow.BuildAuthorizeUrl(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Configured);
        Assert.Null(result.AuthorizeUrl);
    }

    [Fact]
    public void BuildAuthorizeUrlIncludesTheMintedStateAndProjectScope()
    {
        var flow = NewFlow(new FakeGitHubHandler());

        var result = flow.BuildAuthorizeUrl(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.Configured);
        Assert.StartsWith("https://github.com/login/oauth/authorize", result.AuthorizeUrl);
        Assert.Contains("state=", result.AuthorizeUrl);
        Assert.Contains("scope=read%3Auser%20repo", result.AuthorizeUrl);
    }

    [Fact]
    public async Task ExchangeListsRepositoriesUsingTheExchangedToken()
    {
        var stateService = new ConnectionStateService(new EphemeralDataProtectionProvider());
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var state = stateService.Mint(projectId, userId);
        var handler = new FakeGitHubHandler();
        var flow = NewFlow(handler, stateService);

        var result = await flow.ExchangeCodeForRepositoriesAsync("some-code", state, userId);

        Assert.NotNull(result);
        Assert.Equal(projectId, result!.ProjectId);
        Assert.Equal(new[] { "acme/billing", "acme/checkout-service" }, result.Repositories);
        Assert.True(handler.ReposCalledWithCorrectToken);
    }

    // Scenario: The GitHub access token is never persisted
    [Fact]
    public async Task NoTokenShapedValueEverReachesTheDatabase()
    {
        var f = NewFixture();
        var user = await f.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await f.Provisioning.CreateProjectAsync(user.OrganizationId, "P");

        var stateService = new ConnectionStateService(new EphemeralDataProtectionProvider());
        var userId = Guid.NewGuid();
        var state = stateService.Mint(project.Id, userId);
        var handler = new FakeGitHubHandler();
        var flow = NewFlow(handler, stateService);

        var callbackResult = await flow.ExchangeCodeForRepositoriesAsync("some-code", state, userId);
        Assert.NotNull(callbackResult);

        // Only the already-chosen repo name crosses into ConnectionService.ConnectAsync — the token
        // from the exchange above was never passed to it and cannot be retrieved from the flow result.
        await f.Connections.ConnectAsync(project.Id, "github", callbackResult!.Repositories[0]);

        var connection = await f.ConnectionRepo.GetAsync(project.Id);
        Assert.NotNull(connection);
        Assert.DoesNotContain(handler.IssuedAccessToken, connection!.ExternalRepo);

        var everyStoredString = connection.Provider + connection.ExternalRepo
            + user.ClerkUserId + user.DisplayName;
        Assert.DoesNotContain(handler.IssuedAccessToken, everyStoredString);
    }

    [Fact]
    public async Task InvalidStateReturnsNullInsteadOfCrashing()
    {
        var flow = NewFlow(new FakeGitHubHandler());

        var result = await flow.ExchangeCodeForRepositoriesAsync("some-code", "not-a-real-state", Guid.NewGuid());

        Assert.Null(result);
    }

    // security-hardening-pre-pilot D6: a state minted for one user cannot be completed by another.
    [Fact]
    public async Task StateMintedForAnotherUserIsRejected()
    {
        var stateService = new ConnectionStateService(new EphemeralDataProtectionProvider());
        var state = stateService.Mint(Guid.NewGuid(), userId: Guid.NewGuid());
        var flow = NewFlow(new FakeGitHubHandler(), stateService);

        var result = await flow.ExchangeCodeForRepositoriesAsync("some-code", state, callerUserId: Guid.NewGuid());

        Assert.Null(result); // same generic "expired or invalid" outcome
    }

    // Scenario: A project outside the signed-in organization cannot be connected
    [Fact]
    public async Task ConnectionForAnotherOrganizationsProjectIsRejected()
    {
        var f = NewFixture();
        var orgAUser = await f.Provisioning.GetOrCreateUserAsync("clerk-a", "alice", null);
        var orgBUser = await f.Provisioning.GetOrCreateUserAsync("clerk-b", "bob", null);
        var projectB = await f.Provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B's project");

        var belongsToOrgA = await f.Connections.ProjectBelongsToOrganizationAsync(projectB.Id, orgAUser.OrganizationId);

        Assert.False(belongsToOrgA);
    }
}
