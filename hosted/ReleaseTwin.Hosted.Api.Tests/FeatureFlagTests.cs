using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using ReleaseTwin.Hosted.Api.Flags;

namespace ReleaseTwin.Hosted.Api.Tests;

public class FeatureFlagTests
{
    private static FlagRegistry Registry() => FlagRegistry.Load();

    private static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    private static FlagService Service(FeatureProvider provider) =>
        new(provider, Registry(), new StubContextFactory(), NullLogger<FlagService>.Instance);

    private sealed class StubContextFactory : IFlagContextFactory
    {
        public FlagContext Current(ReleaseTwin.Hosted.Api.Data.Entities.Organization? organization = null, Guid? projectId = null) =>
            new(TargetingKey: "org-1", Plan: "free", Surface: "hosted", Env: "development");
    }

    private sealed class ThrowingProvider : FeatureProvider
    {
        public override Metadata GetMetadata() => new("throwing");
        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider boom");
        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider boom");
        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider boom");
        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider boom");
        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider boom");
    }

    [Fact]
    public void Registry_loads_and_contains_the_smoke_flag()
    {
        var registry = Registry();
        Assert.True(registry.TryGet("flag-seam-smoke", out var def));
        Assert.Equal("boolean", def.Type);
        Assert.True(def.BooleanDefault);
        Assert.Contains("hosted", def.Surfaces);
    }

    [Fact]
    public async Task Returns_registry_default_when_no_override()
    {
        var service = Service(new StaticFlagProvider(Registry(), Config()));
        Assert.True(await service.GetBooleanAsync("flag-seam-smoke"));
    }

    [Fact]
    public async Task Config_override_wins_over_registry_default()
    {
        var service = Service(new StaticFlagProvider(Registry(), Config(("FeatureFlags:flag-seam-smoke", "false"))));
        Assert.False(await service.GetBooleanAsync("flag-seam-smoke"));
    }

    [Fact]
    public async Task Unknown_key_returns_the_coded_default_and_never_throws()
    {
        var service = Service(new StaticFlagProvider(Registry(), Config()));
        Assert.False(await service.GetBooleanAsync("no-such-flag", context: null));
        Assert.Equal("", await service.GetStringAsync("no-such-flag"));
        Assert.Equal(0d, await service.GetNumberAsync("no-such-flag"));
    }

    [Fact]
    public async Task Fails_open_to_default_when_provider_throws()
    {
        var service = Service(new ThrowingProvider());
        Assert.True(await service.GetBooleanAsync("flag-seam-smoke"));
    }

    [Fact]
    public void FlagContext_carries_the_shared_shape()
    {
        var ctx = new FlagContext(TargetingKey: "org-9", UserId: "user-3", Plan: "team", ProjectId: "proj-2", Surface: "hosted", Env: "production")
            .ToEvaluationContext();
        Assert.Equal("org-9", ctx.TargetingKey);
        Assert.Equal("team", ctx.GetValue("plan").AsString);
        Assert.Equal("hosted", ctx.GetValue("surface").AsString);
        Assert.Equal("production", ctx.GetValue("env").AsString);
        Assert.Equal("user-3", ctx.GetValue("userId").AsString);
        Assert.Equal("proj-2", ctx.GetValue("projectId").AsString);
    }
}
