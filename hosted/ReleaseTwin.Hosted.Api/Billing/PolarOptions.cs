using Microsoft.Extensions.Configuration;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing: typed Polar configuration, bound inline from <c>IConfiguration</c> in <c>Program.cs</c>
/// (the project's established pattern — no <c>IOptions&lt;T&gt;</c> ceremony). Empty / absent config
/// ⇒ <see cref="IsConfigured"/> is false and the whole billing surface stays closed (safe default,
/// per tasks.md 1.2).
/// </summary>
public sealed class PolarOptions
{
    public const string SectionName = "Polar";

    public string? ApiToken { get; init; }
    public string? WebhookSecret { get; init; }

    /// <summary>Polar REST base, e.g. <c>https://api.polar.sh</c> (or the sandbox host). Defaults to production.</summary>
    public string ApiBaseUrl { get; init; } = "https://api.polar.sh";

    /// <summary>Where Polar sends the buyer after a completed / abandoned checkout.</summary>
    public string? CheckoutSuccessUrl { get; init; }
    public string? CheckoutCancelUrl { get; init; }

    /// <summary>Where Polar sends the customer after they close the portal.</summary>
    public string? PortalReturnUrl { get; init; }

    /// <summary>Polar price id per (tier, cadence). Key format: <c>"Team:Monthly"</c>.</summary>
    public IReadOnlyDictionary<string, string> PriceIds { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// billing (design.md Migration Plan step 4/6): the reconciliation job ships in dry-run — it logs
    /// the corrections it would make and calls nothing. Flipped to false after one clean nightly cycle.
    /// </summary>
    public bool ReconciliationDryRun { get; init; } = true;

    /// <summary>
    /// billing (design.md Migration Plan step 3 vs 5): decouples the customer-facing upgrade button
    /// from the webhook. The webhook endpoint goes live as soon as <see cref="IsConfigured"/> (so Polar
    /// can be registered and events flow), but the dashboard "Upgrade" / "Manage billing" actions stay
    /// closed until this is flipped true — after a real sandbox checkout has been verified end to end.
    /// </summary>
    public bool UpgradeEnabled { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken)
        && !string.IsNullOrWhiteSpace(WebhookSecret)
        && PriceIds.Count > 0;

    /// <summary>Whether the customer-facing upgrade / portal endpoints are open (config present AND explicitly switched on).</summary>
    public bool IsUpgradeEnabled => IsConfigured && UpgradeEnabled;

    public static string PriceKey(PlanTier tier, BillingCadence cadence) => $"{tier}:{cadence}";

    public string? PriceIdFor(PlanTier tier, BillingCadence cadence) =>
        PriceIds.TryGetValue(PriceKey(tier, cadence), out var id) ? id : null;

    /// <summary>Binds a <see cref="PolarOptions"/> from the <c>Polar</c> configuration section.</summary>
    public static PolarOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var priceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetSection("PriceIds").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                priceIds[child.Key] = child.Value;
            }
        }

        return new PolarOptions
        {
            ApiToken = section["ApiToken"],
            WebhookSecret = section["WebhookSecret"],
            ApiBaseUrl = string.IsNullOrWhiteSpace(section["ApiBaseUrl"]) ? "https://api.polar.sh" : section["ApiBaseUrl"]!,
            CheckoutSuccessUrl = section["CheckoutSuccessUrl"],
            CheckoutCancelUrl = section["CheckoutCancelUrl"],
            PortalReturnUrl = section["PortalReturnUrl"],
            PriceIds = priceIds,
            ReconciliationDryRun = !bool.TryParse(section["ReconciliationDryRun"], out var dryRun) || dryRun,
            UpgradeEnabled = bool.TryParse(section["UpgradeEnabled"], out var upgrade) && upgrade,
        };
    }
}
