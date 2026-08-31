using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>Boots the real ASP.NET Core pipeline (auth schemes, endpoints, Razor Pages) against an isolated in-memory hosted-table fake per factory instance.</summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // xUnit's IClassFixture activation requires exactly one public constructor with no parameters —
    // set this property (before the host starts, i.e. before any client/Services access) instead of
    // passing a constructor argument, so tests that need a fake GitHub handler can still supply one.
    public HttpMessageHandler? GitHubConnectionHandlerForTesting { get; init; }

    /// <summary>billing: swapped in for the real HTTP <see cref="IPolarClient"/> so tests never make a network call. Also lets a test set <c>Polar</c> config so <c>PolarOptions.IsConfigured</c> is true.</summary>
    public FakePolarClient PolarClient { get; } = new();

    /// <summary>billing: when true, the host is configured with a fake "Polar" section so the billing endpoints treat billing as configured.</summary>
    public bool ConfigureBilling { get; init; }

    /// <summary>billing: whether the customer-facing upgrade/portal button is switched on. Defaults true so existing endpoint tests stay green; set false to test the "webhook live, button closed" staging state.</summary>
    public bool UpgradeButtonEnabled { get; init; } = true;

    /// <summary>
    /// When true, the real "ClerkJwt" JWT-bearer scheme is swapped for <see cref="TestClerkAuthHandler"/>,
    /// so tests can act as a web session for a specific organization via <see cref="CreateClientForOrg"/>.
    /// </summary>
    public bool UseTestClerkAuth { get; init; }

    /// <summary>An <see cref="HttpClient"/> that authenticates as a web session for <paramref name="organizationId"/> (needs <see cref="UseTestClerkAuth"/>).</summary>
    public HttpClient CreateClientForOrg(Guid organizationId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestClerkAuthHandler.OrgHeader, organizationId.ToString());
        return client;
    }

    /// <summary>org-membership: as <see cref="CreateClientForOrg(Guid)"/> but with an explicit role
    /// (default sessions are Admin).</summary>
    public HttpClient CreateClientForOrg(Guid organizationId, ReleaseTwin.Hosted.Api.Data.Entities.MembershipRole role)
    {
        var client = CreateClientForOrg(organizationId);
        client.DefaultRequestHeaders.Add(TestClerkAuthHandler.RoleHeader, role.ToString());
        return client;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHostedTable));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // A fresh, isolated in-memory table per factory instance — same isolation guarantee the
            // old per-factory EF Core in-memory database name provided.
            services.AddSingleton<IHostedTable, InMemoryHostedTable>();

            if (GitHubConnectionHandlerForTesting is not null)
            {
                services.AddHttpClient("GitHubConnection").ConfigurePrimaryHttpMessageHandler(() => GitHubConnectionHandlerForTesting);
            }

            // billing: never let a test hit the real Polar HTTP client.
            var polarDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPolarClient));
            if (polarDescriptor is not null)
            {
                services.Remove(polarDescriptor);
            }
            services.AddSingleton<IPolarClient>(PolarClient);

            if (UseTestClerkAuth)
            {
                // Repoint the already-registered "ClerkJwt" scheme at the test handler. AddScheme
                // would throw ("scheme already exists"), so mutate the SchemeBuilder in place.
                services.Configure<AuthenticationOptions>(options =>
                {
                    if (options.SchemeMap.TryGetValue("ClerkJwt", out var scheme))
                    {
                        scheme.HandlerType = typeof(TestClerkAuthHandler);
                    }
                });
            }

            if (ConfigureBilling)
            {
                var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PolarOptions));
                if (optionsDescriptor is not null)
                {
                    services.Remove(optionsDescriptor);
                }
                services.AddSingleton(new PolarOptions
                {
                    ApiToken = "test-token",
                    WebhookSecret = "test-webhook-secret",
                    ProductIds = new Dictionary<string, string> { ["Team:Monthly"] = "prod_monthly", ["Team:Annual"] = "prod_annual" },
                    ReconciliationDryRun = false,
                    UpgradeEnabled = UpgradeButtonEnabled,
                });
            }
        });
    }
}
