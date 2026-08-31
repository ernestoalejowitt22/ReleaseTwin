namespace ReleaseTwin.Core;

public sealed record OracleReference(string Locator);

public sealed record FixtureReference(string Locator, string ExpectedSha256, byte[] Content);

public sealed record ResourceKey(string Value);

public sealed record RetryPolicy(int MaxAttempts, TimeSpan? Timeout = null)
{
    public static RetryPolicy Once { get; } = new(1);
}

public sealed record CaptureDeclaration(string Name, string From);

public sealed record PipelineStep(
    string OperationName,
    bool ExpectFailure = false,
    RetryPolicy? Retry = null,
    IReadOnlyDictionary<string, object?>? With = null,
    IReadOnlyList<CaptureDeclaration>? Capture = null)
{
    public RetryPolicy EffectiveRetry => Retry ?? RetryPolicy.Once;
    public IReadOnlyDictionary<string, object?> Parameters => With ?? EmptyParameters;
    public IReadOnlyList<CaptureDeclaration> Captures => Capture ?? EmptyCaptures;
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters = new Dictionary<string, object?>();
    private static readonly IReadOnlyList<CaptureDeclaration> EmptyCaptures = Array.Empty<CaptureDeclaration>();
}

public sealed record PrerequisiteDeclaration(string CheckName, string Owner);

public sealed record CleanupDeclaration(string OperationName);

public sealed record CapabilityRequirement(string Name);

public sealed record TestCase(
    string CaseId,
    OracleReference Oracle,
    FixtureReference Fixture,
    IReadOnlyList<PrerequisiteDeclaration> Prerequisites,
    IReadOnlyList<PipelineStep> Pipeline,
    IReadOnlyList<CleanupDeclaration> Cleanup,
    ResourceKey? ResourceKey = null,
    IReadOnlyList<CapabilityRequirement>? RequiredCapabilities = null)
{
    public IReadOnlyList<CapabilityRequirement> RequiredCapabilities { get; init; } = RequiredCapabilities ?? Array.Empty<CapabilityRequirement>();

    /// <summary>
    /// release-readiness-rollup: an optional free-form label naming the release, sprint, or epic
    /// this case belongs to. Carried for grouping only — it has no effect on execution, eligibility,
    /// flag-proof behavior, or exit code.
    /// </summary>
    public string? Release { get; init; }
}
