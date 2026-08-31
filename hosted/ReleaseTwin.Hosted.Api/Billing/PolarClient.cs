using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing: the concrete HTTP <see cref="IPolarClient"/> against Polar's REST API. Kept deliberately
/// thin — it maps our three operations to Polar endpoints, reads back only the fields we need, and
/// translates any non-success response into <see cref="PolarException"/>. Endpoint paths follow
/// Polar's v1 API (<c>/v1/checkouts/</c>, <c>/v1/customer-portal/sessions/</c>,
/// <c>/v1/subscriptions/{id}</c>); they are the one place a shape change would land.
/// </summary>
public sealed class PolarClient : IPolarClient
{
    private readonly HttpClient _http;
    private readonly PolarOptions _options;
    private readonly ILogger<PolarClient> _logger;

    public PolarClient(HttpClient http, PolarOptions options, ILogger<PolarClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        _http.BaseAddress ??= new Uri(options.ApiBaseUrl);
        _http.DefaultRequestHeaders.Authorization ??= new AuthenticationHeaderValue("Bearer", options.ApiToken);
    }

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(Guid organizationId, PlanTier tier, BillingCadence cadence, CancellationToken cancellationToken = default)
    {
        var priceId = _options.PriceIdFor(tier, cadence)
            ?? throw new PolarException($"No Polar price configured for {tier}/{cadence}.");

        var payload = new
        {
            product_price_id = priceId,
            success_url = _options.CheckoutSuccessUrl,
            // design.md D2: the org id is the only link back — the webhook reads it from checkout metadata.
            metadata = new { organization_id = organizationId.ToString() },
        };

        using var response = await _http.PostAsJsonAsync("/v1/checkouts/", payload, cancellationToken);
        var url = await ReadStringFieldAsync(response, "url", cancellationToken);
        return new CheckoutSession(url);
    }

    public async Task<PortalSession> CreatePortalSessionAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var payload = new { customer_id = customerId, return_url = _options.PortalReturnUrl };
        using var response = await _http.PostAsJsonAsync("/v1/customer-portal/sessions/", payload, cancellationToken);
        var url = await ReadStringFieldAsync(response, "customer_portal_url", cancellationToken);
        return new PortalSession(url);
    }

    public async Task SetSubscriptionQuantityAsync(string subscriptionId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            quantity = 1;
        }

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/subscriptions/{subscriptionId}")
        {
            Content = JsonContent.Create(new { quantity }),
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, cancellationToken);
            _logger.LogWarning("Polar rejected quantity update for {SubscriptionId} → {Quantity}: {Status} {Body}", subscriptionId, quantity, (int)response.StatusCode, body);
            throw new PolarException($"Polar rejected the subscription quantity update ({(int)response.StatusCode}).");
        }
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/v1/subscriptions/{subscriptionId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, cancellationToken);
            throw new PolarException($"Polar returned {(int)response.StatusCode} reading subscription {subscriptionId}: {body}");
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var quantity = root.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qi) ? qi : 1;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            return new SubscriptionInfo(quantity, status);
        }
        catch (JsonException ex)
        {
            throw new PolarException("Polar subscription response was not valid JSON.", ex);
        }
    }

    private static async Task<string> ReadStringFieldAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, cancellationToken);
            throw new PolarException($"Polar returned {(int)response.StatusCode}: {body}");
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()!;
            }

            throw new PolarException($"Polar response is missing the '{field}' field.");
        }
        catch (JsonException ex)
        {
            throw new PolarException("Polar response was not valid JSON.", ex);
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return "<unreadable>";
        }
    }
}
