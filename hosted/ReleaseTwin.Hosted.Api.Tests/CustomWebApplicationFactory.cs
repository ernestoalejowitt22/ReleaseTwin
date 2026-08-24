using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>Boots the real ASP.NET Core pipeline (auth schemes, endpoints, Razor Pages) against an isolated in-memory database per factory instance.</summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    // xUnit's IClassFixture activation requires exactly one public constructor with no parameters —
    // set this property (before the host starts, i.e. before any client/Services access) instead of
    // passing a constructor argument, so tests that need a fake GitHub handler can still supply one.
    public HttpMessageHandler? GitHubConnectionHandlerForTesting { get; init; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HostedDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<HostedDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            if (GitHubConnectionHandlerForTesting is not null)
            {
                services.AddHttpClient("GitHubConnection").ConfigurePrimaryHttpMessageHandler(() => GitHubConnectionHandlerForTesting);
            }
        });
    }
}
