using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.LaunchDarkly;

/// <summary>
/// A LaunchDarkly-backed <see cref="IFeatureStateController"/>, making flag-proof mode usable
/// against systems whose real feature flags live in LaunchDarkly rather than Azure DevOps.
///
/// Credentials are never hardcoded here (adapter-sdk's external-credentials requirement): the
/// caller resolves the API token (environment variable, secret store, etc.) and passes it in via
/// <see cref="LaunchDarklyOptions"/>.
/// </summary>
public sealed class LaunchDarklyAdapter : IAdapterModule, IFeatureStateControllerSource, IDisposable
{
    private readonly LaunchDarklyClient _client;
    private readonly string _flagKey;

    public LaunchDarklyAdapter(
        LaunchDarklyOptions options,
        string flagKey = "release-proof-feature",
        HttpMessageHandler? handler = null)
    {
        _client = new LaunchDarklyClient(options, handler);
        _flagKey = flagKey;
    }

    public string Name => "launchdarkly";

    /// <summary>Exposed so a composition can wire <see cref="ReleaseTwin.Core.FlagProofRunner"/> against this adapter's flag.</summary>
    public IFeatureStateController FeatureStateController => new LaunchDarklyFeatureStateController(_client);

    public static IReadOnlyDictionary<string, string> KnownOperationCapabilities { get; } = new Dictionary<string, string>
    {
        ["ld.readFeatureFlag"] = "http:launchdarkly",
    };

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddOperation("ld.readFeatureFlag", new ReadFeatureFlagOperation(_client, _flagKey))
            .AddCapability("http:launchdarkly")
            .AddCapability("flag-control:runtime");
    }

    public void Dispose() => _client.Dispose();
}
