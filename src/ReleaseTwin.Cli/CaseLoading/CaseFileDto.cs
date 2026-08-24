namespace ReleaseTwin.Cli.CaseLoading;

internal sealed class CaseFileDto
{
    public string? Id { get; set; }
    public OracleDto? Oracle { get; set; }
    public FixtureDto? Fixture { get; set; }
    public List<string>? Requires { get; set; }
    public List<PreconditionDto>? Preconditions { get; set; }
    public List<PipelineStepDto>? Pipeline { get; set; }
    public List<CleanupDto>? Cleanup { get; set; }
    public string? ResourceKey { get; set; }
    public FlagProofDto? FlagProof { get; set; }
}

internal sealed class FlagProofDto
{
    public string? FeatureKey { get; set; }
    public string? BuildIdentity { get; set; }
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
}

internal sealed class CleanupDto
{
    public string? Operation { get; set; }
}
