namespace ReleaseTwin.Core;

/// <summary>
/// evidence-capture: how a single executed pipeline step turned out, recorded in a run's evidence.
/// A closed set fixed by the core so evidence stays comparable across adapters.
/// </summary>
public enum StepEvidenceOutcome
{
    Passed,
    Failed,

    /// <summary>An <c>ExpectFailure</c> step that failed as declared — a pass in effect, distinct from an ordinary pass.</summary>
    ExpectedFailure,

    /// <summary>The step's operation was cancelled by its retry/timeout budget.</summary>
    Timeout,

    /// <summary>The step never ran because an earlier step halted the pipeline.</summary>
    NotExecuted,
}

/// <summary>
/// Vendor-neutral detail of an assertion step: the checked expression and the two values compared.
/// Expected/observed are stringified by the operation — the core does not interpret them.
/// </summary>
public sealed record AssertionDetail(string Expression, string? Expected, string? Observed);

/// <summary>
/// What an operation contributes to a step's evidence when evidence capture is enabled. The core
/// carries <see cref="Adapter"/> opaquely (adapter-defined shape); <see cref="Assertion"/> is the
/// one structured, vendor-neutral concept the core understands.
/// </summary>
public sealed record EvidenceContribution(AssertionDetail? Assertion = null, object? Adapter = null);

/// <summary>One executed (or skipped) pipeline step in a run's evidence.</summary>
public sealed record StepEvidence(
    int Index,
    string OperationName,
    StepEvidenceOutcome Outcome,
    TimeSpan Duration,
    AssertionDetail? Assertion = null,
    object? AdapterEvidence = null);

/// <summary>
/// evidence-capture: the ordered, per-step record of one case execution, produced alongside the
/// <see cref="CaseReport"/> only when evidence capture is enabled for the run. For a flag-proof leg,
/// <see cref="Leg"/> names which leg this is.
/// </summary>
public sealed record RunEvidence(
    string CaseId,
    string OracleLocator,
    IReadOnlyList<StepEvidence> Steps,
    string? Leg = null);

/// <summary>Result of executing a case when the caller asked for evidence — the report plus the optional evidence record.</summary>
public sealed record CaseExecutionResult(CaseReport Report, RunEvidence? Evidence);

/// <summary>Per-run execution knobs. <see cref="CaptureEvidence"/> off ⇒ behavior identical to before evidence existed.</summary>
public sealed record ExecutionOptions
{
    public bool CaptureEvidence { get; init; }

    public static ExecutionOptions Default { get; } = new();
}

/// <summary>
/// Optional contract an <see cref="IOperation"/> implements to contribute structured evidence for
/// the step it just ran. The core calls <see cref="DrainEvidence"/> once after each execution, only
/// when evidence capture is enabled, and expects the operation to return evidence for that call and
/// reset its internal buffer. Operations that do not implement this are unaffected.
/// </summary>
public interface IEvidenceEmittingOperation
{
    EvidenceContribution? DrainEvidence();
}

/// <summary>
/// Thread-safe one-slot buffer an operation can use to stash the evidence for the step it just ran
/// and hand it to the core on the next <see cref="IEvidenceEmittingOperation.DrainEvidence"/> call.
/// </summary>
public sealed class EvidenceBuffer
{
    private readonly object _lock = new();
    private EvidenceContribution? _pending;

    public void Clear() => Set((EvidenceContribution?)null);

    public void Set(EvidenceContribution? contribution)
    {
        lock (_lock)
        {
            _pending = contribution;
        }
    }

    public void SetAdapter(object? adapterEvidence) =>
        Set(adapterEvidence is null ? null : new EvidenceContribution(Adapter: adapterEvidence));

    public EvidenceContribution? Drain()
    {
        lock (_lock)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}
