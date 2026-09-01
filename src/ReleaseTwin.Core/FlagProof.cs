namespace ReleaseTwin.Core;

/// <summary>Sets a feature's runtime state. Implemented by the adapter composition, never by Core.</summary>
public interface IFeatureStateController
{
    Task SetStateAsync(string featureKey, bool enabled, CancellationToken cancellationToken);
}

public enum FlagProofOutcome
{
    /// <summary>Known-bad failed and known-good passed: the oracle correctly discriminates.</summary>
    Passed,

    /// <summary>Both legs passed: the oracle cannot tell the fix apart from the broken state.</summary>
    WeakOracle,

    /// <summary>Both legs failed: distinct from a weak oracle, since neither state satisfies the oracle.</summary>
    BothFailed,

    /// <summary>Known-bad passed and known-good failed: the oracle points the wrong way.</summary>
    Inverted,

    /// <summary>The required feature-state control capability was unavailable; neither leg ran.</summary>
    Ineligible,

    /// <summary>The feature-state control request failed; the run could not be performed.</summary>
    ControlFailed,
}

public sealed record FlagProofResult(
    string CaseId,
    OracleReference Oracle,
    string BuildIdentity,
    FlagProofOutcome Outcome,
    CaseReport? KnownBadLeg,
    CaseReport? KnownGoodLeg,
    string? Message = null);

/// <summary>
/// evidence-capture: a flag-proof result plus the per-leg run evidence, produced only when the
/// caller asked for evidence. Each <see cref="RunEvidence.Leg"/> is "known-bad" or "known-good".
/// </summary>
public sealed record FlagProofExecutionResult(
    FlagProofResult Result,
    RunEvidence? KnownBadEvidence,
    RunEvidence? KnownGoodEvidence);

/// <summary>
/// Runs a case twice against the same fixture and build — once with the target feature known-bad,
/// once known-good — and reports one combined release-proof result.
/// </summary>
public sealed class FlagProofRunner
{
    private readonly CaseExecutor _executor;
    private readonly ICapabilityCatalog _capabilities;
    private readonly IFeatureStateController _featureStateController;

    public FlagProofRunner(CaseExecutor executor, ICapabilityCatalog capabilities, IFeatureStateController featureStateController)
    {
        _executor = executor;
        _capabilities = capabilities;
        _featureStateController = featureStateController;
    }

    public async Task<FlagProofResult> RunAsync(
        TestCase testCase,
        string featureKey,
        string buildIdentity,
        string requiredCapability = "flag-control:runtime",
        CancellationToken cancellationToken = default)
        => (await RunAsync(testCase, featureKey, buildIdentity, ExecutionOptions.Default, requiredCapability, cancellationToken)).Result;

    /// <summary>
    /// Runs the paired legs with explicit options. With <see cref="ExecutionOptions.CaptureEvidence"/>
    /// off, the returned per-leg evidence is null and the <see cref="FlagProofResult"/> is identical
    /// to what the report-only overload produces.
    /// </summary>
    public async Task<FlagProofExecutionResult> RunAsync(
        TestCase testCase,
        string featureKey,
        string buildIdentity,
        ExecutionOptions options,
        string requiredCapability = "flag-control:runtime",
        CancellationToken cancellationToken = default)
    {
        if (!_capabilities.IsAvailable(requiredCapability))
        {
            var ineligible = new FlagProofResult(testCase.CaseId, testCase.Oracle, buildIdentity, FlagProofOutcome.Ineligible, null, null);
            return new FlagProofExecutionResult(ineligible, null, null);
        }

        CaseExecutionResult knownBad;
        CaseExecutionResult knownGood;
        try
        {
            await _featureStateController.SetStateAsync(featureKey, enabled: false, cancellationToken);
            knownBad = await _executor.ExecuteAsync(testCase, options, cancellationToken);

            await _featureStateController.SetStateAsync(featureKey, enabled: true, cancellationToken);
            knownGood = await _executor.ExecuteAsync(testCase, options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var controlFailed = new FlagProofResult(
                testCase.CaseId, testCase.Oracle, buildIdentity, FlagProofOutcome.ControlFailed, null, null,
                $"feature-state control failed: {ex.Message}");
            return new FlagProofExecutionResult(controlFailed, null, null);
        }

        var outcome = (knownBad.Report.Passed, knownGood.Report.Passed) switch
        {
            (false, true) => FlagProofOutcome.Passed,
            (true, true) => FlagProofOutcome.WeakOracle,
            (false, false) => FlagProofOutcome.BothFailed,
            (true, false) => FlagProofOutcome.Inverted,
        };

        var result = new FlagProofResult(testCase.CaseId, testCase.Oracle, buildIdentity, outcome, knownBad.Report, knownGood.Report);
        return new FlagProofExecutionResult(
            result,
            knownBad.Evidence is null ? null : knownBad.Evidence with { Leg = "known-bad" },
            knownGood.Evidence is null ? null : knownGood.Evidence with { Leg = "known-good" });
    }
}
