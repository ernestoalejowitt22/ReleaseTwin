namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>
/// flag-proof-project-template: the optional <c>releasetwin.yml</c> at the root of a cases
/// directory. Holds project-level defaults a case inherits — today just a shared
/// <c>flag_proof.control</c> block. Deserialized strictly (an unknown key is a load error), so a
/// typo like <c>flag_proof.feature_key</c> here is caught rather than silently ignored.
/// </summary>
internal sealed class ProjectManifestDto
{
    public ProjectManifestFlagProofDto? FlagProof { get; set; }
}

/// <summary>
/// The manifest's <c>flag_proof:</c> section. Only <c>control</c> is allowed —
/// <c>feature_key</c> / <c>build_identity</c> stay per-case.
/// </summary>
internal sealed class ProjectManifestFlagProofDto
{
    public FlagProofControlDto? Control { get; set; }
}
