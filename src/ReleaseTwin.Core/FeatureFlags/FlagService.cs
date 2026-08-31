using OpenFeature;
using OpenFeature.Model;

namespace ReleaseTwin.Core.FeatureFlags;

/// <summary>
/// add-feature-flag-seam: the CLI / engine flag-evaluation surface. One provider-agnostic entry
/// point. Every method fails open: a provider error, an unknown key, or a wrong-typed value returns
/// the caller's coded default. Evaluation never throws.
/// </summary>
public interface IFlagService
{
    Task<bool> GetBooleanAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default);

    Task<string> GetStringAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default);

    Task<double> GetNumberAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default);
}

public sealed class FlagService : IFlagService
{
    private readonly FeatureProvider _provider;
    private readonly FlagRegistry _registry;

    public FlagService(FeatureProvider provider, FlagRegistry registry)
    {
        _provider = provider;
        _registry = registry;
    }

    /// <summary>Convenience: the Phase-1 static seam from the embedded registry + optional yaml overrides.</summary>
    public static FlagService Static(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var registry = FlagRegistry.Load();
        return new FlagService(new StaticFlagProvider(registry, overrides), registry);
    }

    public Task<bool> GetBooleanAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) && d.Type == "boolean" ? d.BooleanDefault : false,
            (ctx, def) => _provider.ResolveBooleanValueAsync(key, def, ctx, cancellationToken), context);

    public Task<string> GetStringAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) && d.Type == "string" ? d.StringDefault : "",
            (ctx, def) => _provider.ResolveStringValueAsync(key, def, ctx, cancellationToken), context);

    public Task<double> GetNumberAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) && d.Type == "number" ? d.NumberDefault : 0d,
            (ctx, def) => _provider.ResolveDoubleValueAsync(key, def, ctx, cancellationToken), context);

    private static async Task<T> EvaluateAsync<T>(
        string key,
        T fallback,
        Func<EvaluationContext, T, Task<ResolutionDetails<T>>> resolve,
        FlagContext? context)
    {
        try
        {
            var ctx = (context ?? new FlagContext()).ToEvaluationContext();
            var details = await resolve(ctx, fallback).ConfigureAwait(false);
            return details.ErrorType == OpenFeature.Constant.ErrorType.None ? details.Value : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

/// <summary>
/// add-feature-flag-seam (spec: "shared evaluation-context shape"). Identical attribute names and
/// meanings as the web and hosted surfaces. For the CLI, <see cref="UserId"/> is always absent and
/// <see cref="Surface"/> is always <c>cli</c>.
/// </summary>
public sealed record FlagContext(
    string? TargetingKey = null,
    string Plan = "unknown",
    string? ProjectId = null,
    string Env = "production")
{
    public EvaluationContext ToEvaluationContext()
    {
        var builder = EvaluationContext.Builder()
            .Set("plan", Plan)
            .Set("surface", "cli")
            .Set("env", Env);
        if (!string.IsNullOrEmpty(TargetingKey)) builder.SetTargetingKey(TargetingKey);
        if (!string.IsNullOrEmpty(ProjectId)) builder.Set("projectId", ProjectId);
        return builder.Build();
    }
}
