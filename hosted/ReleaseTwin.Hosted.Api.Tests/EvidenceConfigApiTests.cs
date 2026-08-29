using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class EvidenceConfigApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EvidenceConfigApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CliEndpoint_ReturnsProjectDefaults()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "t", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var config = await client.GetFromJsonAsync<CliConfigDto>("/api/cli/evidence-config");
        Assert.False(config!.CaptureDefault);
        Assert.Equal(30, config.RetentionDays);
    }

    [Fact]
    public async Task CliEndpoint_RejectsWebSessionCredential()
    {
        var client = _factory.CreateClient();
        // No API token at all -> unauthorized (a Clerk JWT would likewise not satisfy the ApiToken scheme).
        var response = await client.GetAsync("/api/cli/evidence-config");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class CliConfigDto
    {
        public bool CaptureDefault { get; set; }
        public int RetentionDays { get; set; }
    }
}
