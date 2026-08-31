using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Plans;

/// <summary>
/// plan-catalog-and-entitlements: the single declarative catalog of commercial tiers and their
/// entitlements, loaded once at startup from the embedded <c>hosted/plans.json</c>. This is the only
/// place a tier limit is defined — every feature gate resolves through <see cref="EntitlementService"/>,
/// never by comparing a <see cref="PlanTier"/> value inline.
/// </summary>
public sealed class PlanCatalog
{
    public required IReadOnlyList<PlanTierDefinition> Tiers { get; init; }

    /// <summary>The tier definition for a stored <see cref="PlanTier"/>, or null if the catalog has no matching id.</summary>
    public PlanTierDefinition? Find(PlanTier tier) =>
        Tiers.FirstOrDefault(t => string.Equals(t.Id, tier.ToString(), StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads and validates the catalog from the assembly's embedded <c>plans.json</c>. Throws on a missing, malformed, or incomplete catalog — never returns a partial one.</summary>
    public static PlanCatalog Load()
    {
        const string resource = "ReleaseTwin.Hosted.Api.Plans.plans.json";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded plan catalog '{resource}' is missing.");

        PlanCatalogDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PlanCatalogDocument>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Plan catalog (plans.json) is not valid JSON.", ex);
        }

        if (document?.Tiers is not { Count: > 0 })
        {
            throw new InvalidOperationException("Plan catalog (plans.json) has no tiers.");
        }

        var expectedIds = new[] { "free", "team", "enterprise" };
        var actualIds = document.Tiers.Select(t => t.Id?.ToLowerInvariant()).ToList();
        if (!expectedIds.SequenceEqual(actualIds))
        {
            throw new InvalidOperationException(
                $"Plan catalog must define exactly [{string.Join(", ", expectedIds)}] in order; found [{string.Join(", ", actualIds)}].");
        }

        var tiers = new List<PlanTierDefinition>(document.Tiers.Count);
        foreach (var tier in document.Tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Name) || tier.Price is null || tier.Entitlements is null || string.IsNullOrWhiteSpace(tier.Support))
            {
                throw new InvalidOperationException($"Plan catalog tier '{tier.Id}' is missing name, price, support, or entitlements.");
            }

            tier.Entitlements.EnsureComplete(tier.Id!);
            tiers.Add(new PlanTierDefinition
            {
                Id = tier.Id!.ToLowerInvariant(),
                Name = tier.Name!,
                Price = new PlanPrice(tier.Price.Amount, tier.Price.Unit ?? "", tier.Price.Placeholder),
                Support = tier.Support!,
                Entitlements = tier.Entitlements.ToEntitlements(),
            });
        }

        return new PlanCatalog { Tiers = tiers };
    }

    // ---- deserialization shapes (private) ----

    private sealed class PlanCatalogDocument
    {
        public List<TierDto>? Tiers { get; set; }
    }

    private sealed class TierDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PriceDto? Price { get; set; }
        public string? Support { get; set; }
        public EntitlementsDto? Entitlements { get; set; }
    }

    private sealed class PriceDto
    {
        public decimal Amount { get; set; }
        public string? Unit { get; set; }
        public bool Placeholder { get; set; }
    }

    private sealed class EntitlementsDto
    {
        public int? MaxProjects { get; set; }
        public bool? EvidenceViewer { get; set; }
        public int? MaxEvidenceRetentionDays { get; set; }
        public bool? CustomRedactionRules { get; set; }
        public bool? ProjectSecrets { get; set; }
        public bool? TrendAnalytics { get; set; }
        public bool? ReleaseRollup { get; set; }
        public bool? CiIntegration { get; set; }
        public bool? Sso { get; set; }
        public bool? AuditLog { get; set; }

        // maxProjects / maxEvidenceRetentionDays are legitimately null ("unlimited" / "custom"); the
        // JSON must still contain the key. A missing bool key deserializes to null here and is the
        // error we want to catch.
        public void EnsureComplete(string tierId)
        {
            var missing = new List<string>();
            if (EvidenceViewer is null) missing.Add("evidenceViewer");
            if (CustomRedactionRules is null) missing.Add("customRedactionRules");
            if (ProjectSecrets is null) missing.Add("projectSecrets");
            if (TrendAnalytics is null) missing.Add("trendAnalytics");
            if (ReleaseRollup is null) missing.Add("releaseRollup");
            if (CiIntegration is null) missing.Add("ciIntegration");
            if (Sso is null) missing.Add("sso");
            if (AuditLog is null) missing.Add("auditLog");
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Plan catalog tier '{tierId}' is missing entitlement(s): {string.Join(", ", missing)}.");
            }
        }

        public Entitlements ToEntitlements() => new(
            MaxProjects: MaxProjects,
            EvidenceViewer: EvidenceViewer!.Value,
            MaxEvidenceRetentionDays: MaxEvidenceRetentionDays,
            CustomRedactionRules: CustomRedactionRules!.Value,
            ProjectSecrets: ProjectSecrets!.Value,
            TrendAnalytics: TrendAnalytics!.Value,
            ReleaseRollup: ReleaseRollup!.Value,
            CiIntegration: CiIntegration!.Value,
            Sso: Sso!.Value,
            AuditLog: AuditLog!.Value);
    }
}

public sealed class PlanTierDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required PlanPrice Price { get; init; }
    public required string Support { get; init; }
    public required Entitlements Entitlements { get; init; }
}

public sealed record PlanPrice(decimal Amount, string Unit, bool Placeholder);

/// <summary>
/// plan-catalog-and-entitlements: a fully-resolved entitlement set. Null numeric values mean
/// "unlimited" (<see cref="MaxProjects"/>) or "custom / bounded only by the system ceiling"
/// (<see cref="MaxEvidenceRetentionDays"/>).
/// </summary>
public sealed record Entitlements(
    int? MaxProjects,
    bool EvidenceViewer,
    int? MaxEvidenceRetentionDays,
    bool CustomRedactionRules,
    bool ProjectSecrets,
    bool TrendAnalytics,
    bool ReleaseRollup,
    bool CiIntegration,
    bool Sso,
    bool AuditLog)
{
    [JsonIgnore]
    public bool HasProjectLimit => MaxProjects is not null;
}
