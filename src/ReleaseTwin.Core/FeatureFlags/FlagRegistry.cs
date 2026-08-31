using System.Reflection;
using System.Text.Json;

namespace ReleaseTwin.Core.FeatureFlags;

/// <summary>
/// add-feature-flag-seam: the engine/CLI view of the single feature-flag registry, <c>flags.json</c> at
/// the repo root (embedded here as <c>ReleaseTwin.Core.FeatureFlags.flags.json</c>). Loaded and validated once at startup — a malformed registry
/// throws here and fails the app rather than yielding a silent empty flag set.
///
/// NOT LaunchDarkly's product-adapter config — this gates ReleaseTwin itself.
/// </summary>
public sealed class FlagRegistry
{
    private static readonly string[] KnownTypes = ["boolean", "string", "number", "object"];
    private static readonly string[] KnownSurfaces = ["web", "hosted", "cli"];

    public required IReadOnlyList<FlagDefinition> Flags { get; init; }

    private readonly Dictionary<string, FlagDefinition> _byKey = new();

    public FlagDefinition this[string key] =>
        _byKey.TryGetValue(key, out var def) ? def : throw new KeyNotFoundException($"flags.json has no flag '{key}'.");

    public bool TryGet(string key, out FlagDefinition definition) => _byKey.TryGetValue(key, out definition!);

    public IEnumerable<FlagDefinition> ForSurface(string surface) => Flags.Where(f => f.Surfaces.Contains(surface));

    /// <summary>Override keys that name no flag in the registry — the CLI warns on these (design Open Question: warn, not ignore silently).</summary>
    public IReadOnlyList<string> UnknownKeys(IEnumerable<string> keys) =>
        keys.Where(k => !_byKey.ContainsKey(k)).ToList();

    public static FlagRegistry Load()
    {
        const string resource = "ReleaseTwin.Core.FeatureFlags.flags.json";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded flag registry '{resource}' is missing.");

        FlagRegistryDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<FlagRegistryDocument>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Feature-flag registry (flags.json) is not valid JSON.", ex);
        }

        if (document?.Flags is not { Count: > 0 })
        {
            throw new InvalidOperationException("Feature-flag registry (flags.json) has no flags.");
        }

        var seen = new HashSet<string>();
        var flags = new List<FlagDefinition>(document.Flags.Count);
        foreach (var raw in document.Flags)
        {
            if (string.IsNullOrWhiteSpace(raw.Key) || !IsKebabCase(raw.Key))
            {
                throw new InvalidOperationException($"flags.json: flag key '{raw.Key}' must be kebab-case.");
            }
            if (!seen.Add(raw.Key))
            {
                throw new InvalidOperationException($"flags.json: duplicate flag key '{raw.Key}'.");
            }
            if (raw.Type is null || !KnownTypes.Contains(raw.Type))
            {
                throw new InvalidOperationException($"flags.json: flag '{raw.Key}' has invalid type '{raw.Type}'.");
            }
            if (raw.Default.ValueKind == JsonValueKind.Undefined || !TypeMatches(raw.Type, raw.Default))
            {
                throw new InvalidOperationException($"flags.json: flag '{raw.Key}' default does not match declared type '{raw.Type}'.");
            }
            if (string.IsNullOrWhiteSpace(raw.Description))
            {
                throw new InvalidOperationException($"flags.json: flag '{raw.Key}' is missing a description.");
            }
            if (string.IsNullOrWhiteSpace(raw.Owner))
            {
                throw new InvalidOperationException($"flags.json: flag '{raw.Key}' is missing an owner.");
            }
            if (raw.Surfaces is not { Count: > 0 } || raw.Surfaces.Any(s => !KnownSurfaces.Contains(s)))
            {
                throw new InvalidOperationException($"flags.json: flag '{raw.Key}' has an invalid surfaces list.");
            }

            flags.Add(new FlagDefinition(raw.Key, raw.Type, raw.Default.Clone(), raw.Description!, raw.Surfaces, raw.Owner!));
        }

        var registry = new FlagRegistry { Flags = flags };
        foreach (var f in flags)
        {
            registry._byKey[f.Key] = f;
        }
        return registry;
    }

    private static bool IsKebabCase(string s) =>
        s.Length > 0 && s.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
        && !s.StartsWith('-') && !s.EndsWith('-') && !s.Contains("--");

    private static bool TypeMatches(string type, JsonElement value) => type switch
    {
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "string" => value.ValueKind is JsonValueKind.String,
        "number" => value.ValueKind is JsonValueKind.Number,
        "object" => value.ValueKind is JsonValueKind.Object,
        _ => false,
    };

    private sealed class FlagRegistryDocument
    {
        public List<FlagDto>? Flags { get; set; }
    }

    private sealed class FlagDto
    {
        public string? Key { get; set; }
        public string? Type { get; set; }
        public JsonElement Default { get; set; }
        public string? Description { get; set; }
        public List<string>? Surfaces { get; set; }
        public string? Owner { get; set; }
    }
}

/// <summary>One registry entry. <see cref="Default"/> is a parsed JSON literal matching <see cref="Type"/>.</summary>
public sealed record FlagDefinition(
    string Key,
    string Type,
    JsonElement Default,
    string Description,
    IReadOnlyList<string> Surfaces,
    string Owner)
{
    public bool BooleanDefault => Default.GetBoolean();
    public string StringDefault => Default.GetString() ?? "";
    public double NumberDefault => Default.GetDouble();
}
