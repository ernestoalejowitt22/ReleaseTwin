using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing: the Merchant-of-Record webhook. Unauthenticated route (Polar has no bearer token to
/// present), gated instead by a Standard Webhooks signature over the raw body. design.md D2: this is
/// the only path that moves an organization's tier / billing status.
/// </summary>
public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/billing/webhook", async (HttpRequest http, PolarOptions options, BillingEventProcessor processor) =>
        {
            if (!options.IsConfigured)
            {
                // Billing surface closed (safe default). Polar is not registered against this env.
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            http.EnableBuffering();
            using var reader = new StreamReader(http.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync(http.HttpContext.RequestAborted);
            http.Body.Position = 0;

            var webhookId = http.Headers["webhook-id"].ToString();
            var timestamp = http.Headers["webhook-timestamp"].ToString();
            var signature = http.Headers["webhook-signature"].ToString();

            if (!BillingWebhookSignature.Verify(options.WebhookSecret, webhookId, timestamp, signature, body))
            {
                // billing spec: missing/invalid signature ⇒ rejected, no state change.
                // security-hardening-pre-pilot D4: a stale/far-future timestamp fails Verify the same
                // way, so a replayed valid delivery lands here too — 401, processor never runs, the
                // dedupe row is never written.
                return Results.Unauthorized();
            }

            BillingEventOutcome outcome;
            try
            {
                outcome = await processor.ProcessAsync(webhookId, body, http.HttpContext.RequestAborted);
            }
            catch (Exception)
            {
                // design.md D3: any failure before the dedupe write ⇒ non-2xx, not recorded, Polar redelivers.
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return outcome switch
            {
                BillingEventOutcome.Processed or BillingEventOutcome.Duplicate => Results.Ok(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        })
        // security-hardening-pre-pilot D7: per-client-address ceiling, evaluated before the handler
        // body so a flood is shed without ever running signature verification.
        .RequireRateLimiting(RateLimiting.BillingWebhookPolicy);
    }
}
