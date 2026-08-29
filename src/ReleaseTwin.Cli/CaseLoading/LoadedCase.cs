using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>A feature key and build identity a case wants toggled for a flag-proof run.</summary>
public sealed record FlagProofDeclaration(string FeatureKey, string BuildIdentity);

/// <summary>A loaded case plus its optional flag-proof declaration — Core's TestCase stays unaware flag-proof exists at the case-file level.</summary>
public sealed record LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof, EvidenceRules Evidence)
{
    public LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof)
        : this(Case, FlagProof, EvidenceRules.None)
    {
    }
}
