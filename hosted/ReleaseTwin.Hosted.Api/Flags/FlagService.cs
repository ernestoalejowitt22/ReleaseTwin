using Microsoft.AspNetCore.Http;
using OpenFeature;
using OpenFeature.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Flags;

/// <summary>
/// add-feature-flag-seam: the hosted flag-evaluation surface. One provider-agnostic entry point —
/// swapping <see cref="FeatureProvider"/> in DI (to LaunchDarkly's OpenFeature provider, say) is the
/// whole migration. Every method fails open: a provider error, an unknown key, or a wrong-typed
/// value returns the caller's coded default. Evaluation never throws.
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
    private readonly IFlagContextFactory _contextFactory;
    private readonly ILogger<FlagService> _logger;

    public FlagService(FeatureProvider provider, FlagRegistry registry, IFlagContextFactory contextFactory, ILogger<FlagService> logger)
    {
        _provider = provider;
        _registry = registry;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public Task<bool> GetBooleanAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) ? d.BooleanDefault : false,
            (ctx, def) => _provider.ResolveBooleanValueAsync(key, def, ctx, cancellationToken), context);

    public Task<string> GetStringAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) ? d.StringDefault : "",
            (ctx, def) => _provider.ResolveStringValueAsync(key, def, ctx, cancellationToken), context);

    public Task<double> GetNumberAsync(string key, FlagContext? context = null, CancellationToken cancellationToken = default) =>
        EvaluateAsync(key, _registry.TryGet(key, out var d) ? d.NumberDefault : 0d,
            (ctx, def) => _provider.ResolveDoubleValueAsync(key, def, ctx, cancellationToken), context);

    private async Task<T> EvaluateAsync<T>(
        string key,
        T fallback,
        Func<EvaluationContext, T, Task<ResolutionDetails<T>>> resolve,
        FlagContext? context)
    {
        try
        {
            var ctx = (context ?? _contextFactory.Current()).ToEvaluationContext();
            var details = await resolve(ctx, fallback).ConfigureAwait(false);
            return details.ErrorType == OpenFeature.Constant.ErrorType.None ? details.Value : fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Feature flag '{FlagKey}' evaluation failed; using coded default.", key);
            return fallback;
        }
    }
}

/// <summary>
/// add-feature-flag-seam (spec: "shared evaluation-context shape"). <see cref="TargetingKey"/> is the
/// organization id where known; the rest mirror the web and CLI surfaces exactly.
/// </summary>
public sealed record FlagContext(
    string? TargetingKey = null,
    string? UserId = null,
    string Plan = "unknown",
    string? ProjectId = null,
    string Surface = "hosted",
    string Env = "production")
{
    public EvaluationContext ToEvaluationContext()
    {
        var builder = EvaluationContext.Builder()
            .Set("plan", Plan)
            .Set("surface", Surface)
            .Set("env", Env);
        if (!string.IsNullOrEmpty(TargetingKey)) builder.SetTargetingKey(TargetingKey);
        if (!string.IsNullOrEmpty(UserId)) builder.Set("userId", UserId);
        if (!string.IsNullOrEmpty(ProjectId)) builder.Set("projectId", ProjectId);
        return builder.Build();
    }
}

public interface IFlagContextFactory
{
    FlagContext Current(Organization? organization = null, Guid? projectId = null);
}

/// <summary>Builds the standard <see cref="FlagContext"/> from the current request's authenticated principal.</summary>
public sealed class FlagContextFactory : IFlagContextFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentOrganizationAccessor _currentOrg;
    private readonly IHostEnvironment _hostEnvironment;

    public FlagContextFactory(IHttpContextAccessor httpContextAccessor, CurrentOrganizationAccessor currentOrg, IHostEnvironment hostEnvironment)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentOrg = currentOrg;
        _hostEnvironment = hostEnvironment;
    }

    public FlagContext Current(Organization? organization = null, Guid? projectId = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return new FlagContext(
            TargetingKey: _currentOrg.OrganizationId?.ToString(),
            UserId: user?.FindFirst("user_id")?.Value,
            Plan: (organization?.PlanTier ?? PlanTier.Free).ToString().ToLowerInvariant(),
            ProjectId: projectId?.ToString(),
            Surface: "hosted",
            Env: _hostEnvironment.IsDevelopment() ? "development" : "production");
    }
}
