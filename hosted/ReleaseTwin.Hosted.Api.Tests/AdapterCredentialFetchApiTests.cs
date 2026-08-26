using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-adapter-credentials: the CLI-facing, API-token-authenticated /api/cli/adapter-credentials fetch — same HTTP-level pattern as IngestApiTests/JourneyFetchApiTests.</summary>
public class AdapterCredentialFetchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdapterCredentialFetchApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<string> SeedTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        return raw;
    }

    private async Task<(string RawToken, Guid ProjectId)> SeedTokenWithCredentialAsync(string adapter, Dictionary<string, string> fields)
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var credentials = scope.ServiceProvider.GetRequiredService<AdapterCredentialService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        await credentials.SetAsync(project.Id, adapter, fields, user.Id.ToString(), user.DisplayName);
        return (raw, project.Id);
    }

    [Fact]
    public async Task MissingTokenIsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cli/adapter-credentials/launchdarkly");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: A fetch with no stored credential is a clear, distinct outcome
    [Fact]
    public async Task ProjectWithNothingConfiguredGetsANotFoundNotAServerError()
    {
        var raw = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetAsync("/api/cli/adapter-credentials/launchdarkly");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Scenario: A valid project token fetches that project's credentials
    [Fact]
    public async Task AValidTokenFetchesItsProjectsStoredCredentials()
    {
        var fields = new Dictionary<string, string> { ["apiToken"] = "api-abc", ["projectKey"] = "proj", ["environmentKey"] = "production" };
        var (raw, _) = await SeedTokenWithCredentialAsync("launchdarkly", fields);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetFromJsonAsync<AdapterCredentialResponse>("/api/cli/adapter-credentials/launchdarkly");

        Assert.NotNull(response);
        Assert.Equal("api-abc", response!.Fields["apiToken"]);
        Assert.Equal("proj", response.Fields["projectKey"]);
        Assert.Equal("production", response.Fields["environmentKey"]);
    }

    // Scenario: A wrong-project token cannot fetch another project's credentials
    [Fact]
    public async Task ATokenFromADifferentProjectCannotFetchTheseCredentials()
    {
        var fields = new Dictionary<string, string> { ["apiToken"] = "api-abc", ["projectKey"] = "proj", ["environmentKey"] = "production" };
        await SeedTokenWithCredentialAsync("launchdarkly", fields);
        var otherToken = await SeedTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await client.GetAsync("/api/cli/adapter-credentials/launchdarkly");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
