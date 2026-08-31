using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// plan-catalog-and-entitlements: unit tests that construct services directly (rather than through
/// the DI container in <see cref="CustomWebApplicationFactory"/>) need an <see cref="IEntitlementService"/>.
/// This wires one up over the real embedded <c>plans.json</c>, so tests exercise the actual catalog.
/// </summary>
internal static class TestEntitlements
{
    public static readonly PlanCatalog Catalog = PlanCatalog.Load();

    public static IEntitlementService Service { get; } =
        new EntitlementService(Catalog, NullLogger<EntitlementService>.Instance);
}
