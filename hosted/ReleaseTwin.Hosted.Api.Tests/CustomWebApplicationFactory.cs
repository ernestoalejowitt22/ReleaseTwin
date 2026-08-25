using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>Boots the real ASP.NET Core pipeline (auth schemes, endpoints, Razor Pages) against an isolated in-memory hosted-table fake per factory instance.</summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // xUnit's IClassFixture activation requires exactly one public constructor with no parameters —
    // set this property (before the host starts, i.e. before any client/Services access) instead of
    // passing a constructor argument, so tests that need a fake GitHub handler can still supply one.
    public HttpMessageHandler? GitHubConnectionHandlerForTesting { get; init; }

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
        });
    }
}
