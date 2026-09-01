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
    FlagProofPolarity Polarity);

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
