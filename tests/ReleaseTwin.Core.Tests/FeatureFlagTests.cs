using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using ReleaseTwin.Core.FeatureFlags;

namespace ReleaseTwin.Core.Tests;

public class FeatureFlagTests
{
    private static FlagRegistry Registry() => FlagRegistry.Load();

    private sealed class ThrowingProvider : FeatureProvider
    {
        public override Metadata GetMetadata() => new("throwing");
        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Embedded_registry_loads_and_validates()
    {
        var registry = Registry();
        Assert.NotEmpty(registry.Flags);
        Assert.True(registry.TryGet("flag-seam-smoke", out var def));
        Assert.Equal("boolean", def.Type);
        Assert.True(def.BooleanDefault);
        Assert.Contains("cli", def.Surfaces);
    }

    [Fact]
    public async Task Offline_run_resolves_every_flag_to_its_default()
    {
        var service = FlagService.Static();
        foreach (var def in Registry().Flags)
        {
            if (def.Type == "boolean")
            {
                Assert.Equal(def.BooleanDefault, await service.GetBooleanAsync(def.Key));
            }
        }
    }

    [Fact]
    public async Task Yaml_override_wins_over_the_registry_default()
    {
        var service = FlagService.Static(new Dictionary<string, string?> { ["flag-seam-smoke"] = "false" });
        Assert.False(await service.GetBooleanAsync("flag-seam-smoke"));
    }

    [Fact]
    public async Task Unknown_key_returns_coded_default_and_never_throws()
    {
        var service = FlagService.Static();
        Assert.False(await service.GetBooleanAsync("no-such-flag"));
        Assert.Equal("", await service.GetStringAsync("no-such-flag"));
    }

    [Fact]
    public async Task Fails_open_when_the_provider_throws()
    {
        var service = new FlagService(new ThrowingProvider(), Registry());
        Assert.True(await service.GetBooleanAsync("flag-seam-smoke"));
    }

    [Fact]
    public void Unknown_override_keys_are_reported()
    {
        var unknown = Registry().UnknownKeys(new[] { "flag-seam-smoke", "totally-made-up" });
        Assert.Equal(new[] { "totally-made-up" }, unknown);
    }

    [Fact]
    public void Flag_context_is_the_shared_shape_with_cli_surface()
    {
        var ctx = new FlagContext(TargetingKey: "org-1", ProjectId: "proj-2", Plan: "team", Env: "production").ToEvaluationContext();
        Assert.Equal("org-1", ctx.TargetingKey);
        Assert.Equal("cli", ctx.GetValue("surface").AsString);
        Assert.Equal("team", ctx.GetValue("plan").AsString);
        Assert.Equal("proj-2", ctx.GetValue("projectId").AsString);
    }
}
