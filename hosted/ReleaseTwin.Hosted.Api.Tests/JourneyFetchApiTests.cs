using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-journeys: the CLI-facing, API-token-authenticated /api/cli/journeys fetch — same HTTP-level pattern as IngestApiTests.</summary>
public class JourneyFetchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JourneyFetchApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(string RawToken, Guid ProjectId, Guid JourneyId, int Version)> SeedJourneyAsync(string yamlContent = "pipeline: []")
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "Test Project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        var journey = await journeys.CreateJourneyAsync(project.Id, "My Journey");
        var version = await journeys.CreateVersionAsync(journey.Id, yamlContent, user.Id.ToString(), user.DisplayName);

        return (raw, project.Id, journey.Id, version.Version);
    }

    [Fact]
    public async Task MissingTokenIsRejected()
    {
        var (_, _, journeyId, version) = await SeedJourneyAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/cli/journeys/{journeyId}/versions/{version}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: A fetch with no version specified is rejected, not resolved to "latest"
    [Fact]
    public async Task FetchingWithoutASpecificVersionIsRejected()
    {
        var (raw, _, journeyId, _) = await SeedJourneyAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetAsync($"/api/cli/journeys/{journeyId}/versions/latest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ATokenFromADifferentProjectCannotFetchTheJourney()
    {
        var (_, _, journeyId, version) = await SeedJourneyAsync();

        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var otherUser = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "other", null);
        var otherProject = await provisioning.CreateProjectAsync(otherUser.OrganizationId, "Other Project");
        var (_, otherRaw) = await provisioning.IssueTokenAsync(otherProject.Id, otherUser.OrganizationId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherRaw);

        var response = await client.GetAsync($"/api/cli/journeys/{journeyId}/versions/{version}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Scenario: A pinned run is reproducible
    [Fact]
    public async Task TwoFetchesOfTheSameVersionReturnIdenticalContent()
    {
        var (raw, _, journeyId, version) = await SeedJourneyAsync("pipeline:\n  - operation: http.request");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var first = await client.GetFromJsonAsync<JourneyVersionResponse>($"/api/cli/journeys/{journeyId}/versions/{version}");
        var second = await client.GetFromJsonAsync<JourneyVersionResponse>($"/api/cli/journeys/{journeyId}/versions/{version}");

        Assert.Equal(first!.YamlContent, second!.YamlContent);
        Assert.Equal("pipeline:\n  - operation: http.request", first.YamlContent);
    }

    // Scenario: Editing a journey does not alter a previously fetched version
    [Fact]
    public async Task EditingAfterAFetchDoesNotAlterTheAlreadyFetchedVersion()
    {
        var (raw, _, journeyId, version) = await SeedJourneyAsync("original content");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var beforeEdit = await client.GetFromJsonAsync<JourneyVersionResponse>($"/api/cli/journeys/{journeyId}/versions/{version}");

        using (var scope = _factory.Services.CreateScope())
        {
            var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();
            await journeys.CreateVersionAsync(journeyId, "edited content", "someone", "Someone");
        }

        var afterEdit = await client.GetFromJsonAsync<JourneyVersionResponse>($"/api/cli/journeys/{journeyId}/versions/{version}");

        Assert.Equal("original content", beforeEdit!.YamlContent);
        Assert.Equal("original content", afterEdit!.YamlContent);
    }

    [Fact]
    public async Task FetchingAnUnknownVersionIsNotFound()
    {
        var (raw, _, journeyId, _) = await SeedJourneyAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetAsync($"/api/cli/journeys/{journeyId}/versions/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
