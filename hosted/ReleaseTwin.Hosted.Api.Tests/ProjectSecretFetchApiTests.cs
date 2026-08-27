using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-project-secrets: the CLI-facing, API-token-authenticated /api/cli/project-secrets fetch — same HTTP-level pattern as AdapterCredentialFetchApiTests.</summary>
public class ProjectSecretFetchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectSecretFetchApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<string> SeedTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        return raw;
    }

    private async Task<string> SeedTokenWithSecretAsync(string name, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var secrets = scope.ServiceProvider.GetRequiredService<ProjectSecretService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        // Storing a secret requires the Paid tier (project-secrets spec) — irrelevant to fetching,
        // but the seed has to get past it to have anything stored to fetch in the first place.
        await provisioning.UpgradeOrganizationAsync(user.OrganizationId);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        await secrets.SetAsync(user.OrganizationId, project.Id, name, value, user.Id.ToString(), user.DisplayName);
        return raw;
    }

    [Fact]
    public async Task MissingTokenIsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cli/project-secrets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: A project with no stored secrets is a clear, distinct outcome
    [Fact]
    public async Task ProjectWithNothingConfiguredGetsAnEmptySetNotAnError()
    {
        var raw = await SeedTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetFromJsonAsync<ProjectSecretsResponse>("/api/cli/project-secrets");

        Assert.NotNull(response);
        Assert.Empty(response!.Secrets);
    }

    // Scenario: A valid project token fetches that project's secrets
    [Fact]
    public async Task AValidTokenFetchesItsProjectsStoredSecrets()
    {
        var raw = await SeedTokenWithSecretAsync("NAHA_E2E_SECRET", "real-value");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetFromJsonAsync<ProjectSecretsResponse>("/api/cli/project-secrets");

        Assert.NotNull(response);
        Assert.Equal("real-value", response!.Secrets["NAHA_E2E_SECRET"]);
    }

    // Scenario: A wrong-project token cannot fetch another project's secrets
    [Fact]
    public async Task ATokenFromADifferentProjectCannotFetchTheseSecrets()
    {
        await SeedTokenWithSecretAsync("NAHA_E2E_SECRET", "real-value");
        var otherToken = await SeedTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await client.GetFromJsonAsync<ProjectSecretsResponse>("/api/cli/project-secrets");

        Assert.NotNull(response);
        Assert.Empty(response!.Secrets);
    }
}
