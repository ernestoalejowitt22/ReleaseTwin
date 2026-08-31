using System.Text.Json;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace ReleaseTwin.Core.FeatureFlags;

/// <summary>
/// add-feature-flag-seam (design D1/D3): the Phase-1 OpenFeature provider for the CLI / engine —
/// resolves every flag from the embedded <see cref="FlagRegistry"/> defaults, with a per-key
/// override from the <c>featureFlags:</c> map in <c>releasetwin.yaml</c>. Fully in-process, no
/// network: an air-gapped CI run resolves every flag.
///
/// This is the seam. Adopting an external provider later means constructing a different
/// <see cref="FeatureProvider"/> in the CLI composition root; nothing calling
/// <see cref="IFlagService"/> changes.
/// </summary>
public sealed class StaticFlagProvider : FeatureProvider
{
    private readonly Dictionary<string, ResolvedFlag> _values = new();

    public StaticFlagProvider(FlagRegistry registry, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        foreach (var def in registry.Flags)
        {
            var text = overrides is not null && overrides.TryGetValue(def.Key, out var t) ? t : null;
            _values[def.Key] = Resolve(def, text);
        }
    }

    public override Metadata GetMetadata() => new("releasetwin-static-registry");

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pick(flagKey, defaultValue, v => v.Boolean is { }, v => v.Boolean!.Value));

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pick(flagKey, defaultValue, v => v.String is not null, v => v.String!));

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pick(flagKey, defaultValue, v => v.Number is { }, v => Convert.ToInt32(v.Number!.Value)));

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pick(flagKey, defaultValue, v => v.Number is { }, v => v.Number!.Value));

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pick(flagKey, defaultValue, v => v.Structure is not null, v => v.Structure!));

    private ResolutionDetails<T> Pick<T>(string flagKey, T defaultValue, Func<ResolvedFlag, bool> has, Func<ResolvedFlag, T> get)
    {
        if (!_values.TryGetValue(flagKey, out var resolved))
        {
            return new ResolutionDetails<T>(flagKey, defaultValue, ErrorType.None, Reason.Default);
        }

        return has(resolved)
            ? new ResolutionDetails<T>(flagKey, get(resolved), ErrorType.None, Reason.Static)
            : new ResolutionDetails<T>(flagKey, defaultValue, ErrorType.TypeMismatch, Reason.Error);
    }

    private static ResolvedFlag Resolve(FlagDefinition def, string? overrideText)
    {
        switch (def.Type)
        {
            case "boolean":
            {
                var value = def.BooleanDefault;
                if (bool.TryParse(overrideText, out var o)) value = o;
                return new ResolvedFlag { Boolean = value };
            }
            case "string":
                return new ResolvedFlag { String = overrideText ?? def.StringDefault };
            case "number":
            {
                var value = def.NumberDefault;
                if (double.TryParse(overrideText, out var o)) value = o;
                return new ResolvedFlag { Number = value };
            }
            case "object":
            {
                var json = overrideText ?? def.Default.GetRawText();
                try
                {
                    return new ResolvedFlag { Structure = JsonValue(JsonDocument.Parse(json).RootElement) };
                }
                catch (JsonException)
                {
                    return new ResolvedFlag { Structure = JsonValue(def.Default) };
                }
            }
            default:
                throw new InvalidOperationException($"Unsupported flag type '{def.Type}'.");
        }
    }

    private static Value JsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => new Value(element.GetBoolean()),
        JsonValueKind.Number => new Value(element.GetDouble()),
        JsonValueKind.String => new Value(element.GetString() ?? ""),
        JsonValueKind.Array => new Value(element.EnumerateArray().Select(JsonValue).ToList()),
        JsonValueKind.Object => new Value(new Structure(element.EnumerateObject().ToDictionary(p => p.Name, p => JsonValue(p.Value)))),
        _ => new Value(),
    };

    private sealed class ResolvedFlag
    {
        public bool? Boolean { get; init; }
        public string? String { get; init; }
        public double? Number { get; init; }
        public Value? Structure { get; init; }
    }
}
