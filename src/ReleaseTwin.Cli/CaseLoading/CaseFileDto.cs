namespace ReleaseTwin.Cli.CaseLoading;

internal sealed class CaseFileDto
{
    public string? Id { get; set; }

    /// <summary>release-readiness-rollup: free-form; loosely typed so a non-scalar value gets a clear error rather than a raw YAML exception.</summary>
    public object? Release { get; set; }

    public OracleDto? Oracle { get; set; }
    public FixtureDto? Fixture { get; set; }
    public List<string>? Requires { get; set; }
    public List<PreconditionDto>? Preconditions { get; set; }
    public List<PipelineStepDto>? Pipeline { get; set; }
    public List<CleanupDto>? Cleanup { get; set; }
    public string? ResourceKey { get; set; }
    public FlagProofDto? FlagProof { get; set; }
    public EvidenceDto? Evidence { get; set; }
}

internal sealed class EvidenceDto
{
    public List<string>? Capture { get; set; }
    public List<EvidenceRedactDto>? Redact { get; set; }
}

internal sealed class EvidenceRedactDto
{
    public string? Header { get; set; }
    public string? JsonPath { get; set; }
    public string? Field { get; set; }
    public string? Selector { get; set; }
    public string? Region { get; set; }
}

internal sealed class FlagProofDto
{
    public string? FeatureKey { get; set; }
    public string? BuildIdentity { get; set; }
    public FlagProofControlDto? Control { get; set; }
}

internal sealed class FlagProofControlDto
{
    public string? Method { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public string? KnownBadWhen { get; set; }
    public FlagProofControlVerifyDto? Verify { get; set; }
    public FlagProofControlAuthDto? Auth { get; set; }
}

internal sealed class FlagProofControlAuthDto
{
    public FlagProofOauth2ClientCredentialsDto? Oauth2ClientCredentials { get; set; }
}

internal sealed class FlagProofOauth2ClientCredentialsDto
{
    public string? TokenUrl { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
}

internal sealed class FlagProofControlVerifyDto
{
    public string? Method { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public string? JsonPath { get; set; }
    public string? Expected { get; set; }
}

internal sealed class OracleDto
{
    public string? Locator { get; set; }
}

internal sealed class FixtureDto
{
    public string? Locator { get; set; }
    public string? Sha256 { get; set; }
}

internal sealed class PreconditionDto
{
    public string? Check { get; set; }
    public string? Owner { get; set; }
}

internal sealed class PipelineStepDto
{
    public string? Operation { get; set; }
    public object? With { get; set; }
    public List<CaptureDto>? Capture { get; set; }
}

internal sealed class CaptureDto
{
    public string? Name { get; set; }
    public string? From { get; set; }
}

internal sealed class CleanupDto
{
    public string? Operation { get; set; }
}
