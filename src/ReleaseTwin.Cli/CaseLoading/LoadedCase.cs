using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>http-flag-control: which real flag state is the buggy one.</summary>
public enum FlagProofPolarity
{
    /// <summary>The known-bad leg drives the flag OFF, the known-good leg ON. Default.</summary>
    KnownBadWhenDisabled,

    /// <summary>Inverted: the known-bad leg drives the flag ON, the known-good leg OFF.</summary>
    KnownBadWhenEnabled,
}

/// <summary>
/// http-flag-control: one config-declared HTTP request that sets the target feature's state.
/// <c>${ENV_VAR}</c> is already resolved (load time); <c>{{featureKey}}</c> / <c>{{state}}</c> /
/// <c>{{enabled}}</c> are substituted per leg by the controller.
/// </summary>
public sealed record FlagProofControl(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    FlagProofPolarity Polarity,
    FlagProofControlVerify? Verify = null,
    FlagProofControlAuth? Auth = null);

/// <summary>
/// http-flag-control: an optional OAuth2 client-credentials exchange performed before the control
/// request for each leg, so a flag API gated by Entra ID / org OAuth is reachable. The captured
/// access token is substituted for <c>{{token}}</c> in the control request. <c>${ENV_VAR}</c> is
/// already resolved (load time); the case file holds only references, never a literal secret.
/// </summary>
public sealed record FlagProofControlAuth(
    string TokenUrl,
    string ClientId,
    string ClientSecret,
    string? Scope);

/// <summary>
/// http-flag-control: an optional read-back performed after the control request and before the leg,
/// confirming the flag actually reached the intended state. <c>${ENV_VAR}</c> is already resolved
/// (load time); <c>{{featureKey}}</c> / <c>{{state}}</c> / <c>{{enabled}}</c> are substituted per
/// leg by the controller, including inside <c>Expected</c>. A null <c>Headers</c> means "reuse the
/// control block's headers".
/// </summary>
public sealed record FlagProofControlVerify(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string>? Headers,
    string? Body,
    string JsonPath,
    string Expected);

/// <summary>A feature key and build identity a case wants toggled for a flag-proof run, and optionally
/// how to toggle it over HTTP when no adapter provides a feature-state controller.</summary>
public sealed record FlagProofDeclaration(string FeatureKey, string BuildIdentity, FlagProofControl? Control = null);

/// <summary>A loaded case plus its optional flag-proof declaration — Core's TestCase stays unaware flag-proof exists at the case-file level.</summary>
public sealed record LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof, EvidenceRules Evidence)
{
    public LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof)
        : this(Case, FlagProof, EvidenceRules.None)
    {
    }
}
