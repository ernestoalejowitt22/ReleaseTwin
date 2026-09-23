namespace ReleaseTwin.Core;

public sealed record OracleReference(string Locator);

public sealed record FixtureReference(string Locator, string ExpectedSha256, byte[] Content);

public sealed record ResourceKey(string Value);

public sealed record RetryPolicy(int MaxAttempts, TimeSpan? Timeout = null)
{
    public static RetryPolicy Once { get; } = new(1);
}

public sealed record CaptureDeclaration(string Name, string From);

/// <summary>
/// journey-branching: one entry in a case's pipeline — either a <see cref="PipelineStep"/> or a
/// <see cref="ChoiceStep"/>. A linear pipeline is simply a list containing only steps.
/// </summary>
public interface IPipelineEntry
{
}

/// <summary>journey-branching: the operators a <see cref="Condition"/> may use.</summary>
public enum ConditionOperator
{
    /// <summary>The captured value equals <see cref="Condition.Value"/> (ordinal).</summary>
    Equals,

    /// <summary>The captured value does not equal <see cref="Condition.Value"/>, or was never captured.</summary>
    NotEquals,

    /// <summary>The capture is present and non-empty.</summary>
    Exists,

    /// <summary>The capture is absent or empty.</summary>
    NotExists,
}

/// <summary>
/// journey-branching: a condition over an already-captured value, shared by a step's
/// <see cref="PipelineStep.When"/> guard and a <see cref="ChoiceStep"/>'s branch condition.
/// <see cref="Ref"/> is a capture name (no <c>{{ }}</c>); <see cref="Value"/> is ignored by the
/// unary <see cref="ConditionOperator.Exists"/>/<see cref="ConditionOperator.NotExists"/>.
/// </summary>
public sealed record Condition(string Ref, ConditionOperator Operator, string? Value = null);

/// <summary>
/// journey-branching: a two-way branch. Exactly one of <see cref="Then"/>/<see cref="Else"/> runs;
/// every step of the other records <c>NotExecuted</c>. Branches hold plain steps only — the type
/// makes a choice nested inside a branch unrepresentable.
/// </summary>
public sealed record ChoiceStep(
    Condition When,
    IReadOnlyList<PipelineStep> Then,
    IReadOnlyList<PipelineStep> Else) : IPipelineEntry;

public sealed record PipelineStep(
    string OperationName,
    bool ExpectFailure = false,
    RetryPolicy? Retry = null,
    IReadOnlyDictionary<string, object?>? With = null,
    IReadOnlyList<CaptureDeclaration>? Capture = null,
    Condition? When = null) : IPipelineEntry
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
    IReadOnlyList<IPipelineEntry> Pipeline,
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

public static class PipelineEntries
{
    /// <summary>
    /// journey-branching: every step of a pipeline in structural (pre-order) position — a top-level
    /// step, then a choice's <c>then</c> steps followed by its <c>else</c> steps. A step's position in
    /// this list is its evidence index, identical for every run of the same pipeline regardless of
    /// which branch is taken.
    /// </summary>
    public static IReadOnlyList<PipelineStep> FlattenSteps(this IReadOnlyList<IPipelineEntry> pipeline)
    {
        var steps = new List<PipelineStep>();
        foreach (var entry in pipeline)
        {
            switch (entry)
            {
                case PipelineStep step:
                    steps.Add(step);
                    break;
                case ChoiceStep choice:
                    steps.AddRange(choice.Then);
                    steps.AddRange(choice.Else);
                    break;
                default:
                    throw new ArgumentException($"Unsupported pipeline entry type '{entry?.GetType().Name}'", nameof(pipeline));
            }
        }

        return steps;
    }
}
