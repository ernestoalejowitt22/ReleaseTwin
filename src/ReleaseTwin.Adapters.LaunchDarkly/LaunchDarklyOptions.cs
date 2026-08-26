namespace ReleaseTwin.Adapters.LaunchDarkly;

/// <summary>Credentials supplied externally by the caller (never hardcoded), per adapter-sdk convention.</summary>
public sealed record LaunchDarklyOptions(string ApiToken, string ProjectKey, string EnvironmentKey);
