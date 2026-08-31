using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// plan-catalog-and-entitlements: <c>GET /plans</c> — the plan catalog, unauthenticated and
/// cacheable, with no caller-specific data. The dashboard's live upgrade UI reads it; the marketing
/// site consumes <c>hosted/plans.json</c> directly at build time.
/// </summary>
public static class PlansEndpoints
{
    public static void MapPlansEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/plans", (IEntitlementService entitlements, HttpResponse response) =>
        {
            response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(new
            {
                tiers = entitlements.Catalog.Tiers.Select(t => new
                {
                    t.Id,
                    t.Name,
                    price = new { t.Price.Amount, t.Price.Unit, t.Price.Placeholder },
                    t.Support,
                    t.Entitlements,
                }),
            });
        });
    }
}
