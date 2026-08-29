using System.Diagnostics;
using System.Security.Cryptography;

namespace ReleaseTwin.Core;

public sealed class CaseExecutor
{
    private readonly IOperationCatalog _operations;
    private readonly IPrerequisiteCatalog _prerequisites;
    private readonly ICleanupCatalog _cleanups;
    private readonly ICapabilityCatalog _capabilities;
    private readonly IResourceSerializer _resourceSerializer;

    public CaseExecutor(
        IOperationCatalog operations,
        IPrerequisiteCatalog prerequisites,
        ICleanupCatalog cleanups,
        ICapabilityCatalog capabilities,
        IResourceSerializer? resourceSerializer = null)
    {
        _operations = operations;
        _prerequisites = prerequisites;
        _cleanups = cleanups;
        _capabilities = capabilities;
        _resourceSerializer = resourceSerializer ?? new SemaphoreResourceSerializer();
    }

    /// <summary>Runs a case and returns only its report — unchanged from before evidence capture existed.</summary>
    public async Task<CaseReport> ExecuteAsync(TestCase testCase, CancellationToken cancellationToken = default)
        => (await ExecuteAsync(testCase, ExecutionOptions.Default, cancellationToken)).Report;

    /// <summary>
    /// Runs a case with explicit options. When <see cref="ExecutionOptions.CaptureEvidence"/> is
    /// false the returned <see cref="CaseExecutionResult.Evidence"/> is null and the report is
    /// byte-for-byte what the report-only overload produces.
    /// </summary>
    public async Task<CaseExecutionResult> ExecuteAsync(TestCase testCase, ExecutionOptions options, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var capture = options.CaptureEvidence;
        var context = new CaseExecutionContext { Case = testCase, CaptureEvidence = capture };

        var resourceLock = testCase.ResourceKey is null
            ? null
            : await _resourceSerializer.AcquireAsync(testCase.ResourceKey, cancellationToken);

        try
        {
            foreach (var capability in testCase.RequiredCapabilities)
            {
                if (!_capabilities.IsAvailable(capability.Name))
                {
                    return Result(testCase, stopwatch, passed: false, FailureClassification.Infrastructure, $"missing-capability:{capability.Name}", CleanupStatus.NotRun, capture, AllNotExecuted(testCase, capture));
                }
            }

            // Required capabilities must be confirmed available before validating references: a case
            // whose gated capability isn't installed should report missing-capability, not crash on an
            // unknown operation/prerequisite/cleanup name that capability would have explained.
            ValidateReferences(testCase);

            if (!VerifyFixture(testCase.Fixture))
            {
                return Result(testCase, stopwatch, passed: false, FailureClassification.Infrastructure, "fixture-integrity-mismatch", CleanupStatus.NotRun, capture, AllNotExecuted(testCase, capture));
            }

            foreach (var declaration in testCase.Prerequisites)
            {
                _prerequisites.TryGet(declaration.CheckName, out var check);
                var result = await check!.EvaluateAsync(context, cancellationToken);
                if (result.Status == PrerequisiteStatus.Satisfied)
                {
                    continue;
                }

                // NotSatisfied is a confirmed prerequisite gap; Inconclusive means the check itself
                // could not run (e.g. an unreachable dependency) — these must not be reported the same way.
                var prerequisiteClassification = result.Status == PrerequisiteStatus.NotSatisfied
                    ? FailureClassification.Prerequisite
                    : FailureClassification.Infrastructure;
                var haltedCleanupStatus = await RunCleanupAsync(testCase, context, cancellationToken);
                return Result(testCase, stopwatch, passed: false, prerequisiteClassification, result.Detail, haltedCleanupStatus, capture, AllNotExecuted(testCase, capture));
            }

            var (passed, classification, detail, steps) = await RunPipelineAsync(testCase, context, capture, cancellationToken);
            var cleanupStatus = await RunCleanupAsync(testCase, context, cancellationToken);
            return Result(testCase, stopwatch, passed, classification, detail, cleanupStatus, capture, steps);
        }
        finally
        {
            resourceLock?.Dispose();
        }
    }

    private async Task<(bool Passed, FailureClassification? Classification, string? Detail, List<StepEvidence>? Steps)> RunPipelineAsync(
        TestCase testCase, CaseExecutionContext context, bool capture, CancellationToken cancellationToken)
    {
        var steps = capture ? new List<StepEvidence>() : null;

        for (var i = 0; i < testCase.Pipeline.Count; i++)
        {
            var step = testCase.Pipeline[i];
            _operations.TryGet(step.OperationName, out var operation);

            IReadOnlyDictionary<string, object?> resolvedParameters;
            try
            {
                resolvedParameters = CaptureReferenceResolver.Resolve(step.Parameters, (IReadOnlyDictionary<string, string>)context.Captures);
            }
            catch (MissingCaptureException ex)
            {
                RecordStep(steps, i, step, StepEvidenceOutcome.Failed, TimeSpan.Zero, operation, capture);
                MarkRemainingNotExecuted(steps, testCase, i + 1, capture);
                return (false, FailureClassification.Infrastructure, $"missing-capture:{ex.CaptureName}", steps);
            }

            var stepStopwatch = capture ? Stopwatch.StartNew() : null;
            var (succeeded, stepDetail, isTimeout, stepCaptures) =
                await ExecuteStepWithRetryAsync(operation!, context, step, resolvedParameters, cancellationToken);
            stepStopwatch?.Stop();

            if (succeeded)
            {
                foreach (var (name, value) in stepCaptures)
                {
                    context.Captures[name] = value;
                }
            }

            var effectivelyPassed = step.ExpectFailure ? !succeeded : succeeded;

            var outcome = effectivelyPassed
                ? (step.ExpectFailure ? StepEvidenceOutcome.ExpectedFailure : StepEvidenceOutcome.Passed)
                : isTimeout ? StepEvidenceOutcome.Timeout
                : step.ExpectFailure ? StepEvidenceOutcome.Failed
                : StepEvidenceOutcome.Failed;
            RecordStep(steps, i, step, outcome, stepStopwatch?.Elapsed ?? TimeSpan.Zero, operation, capture);

            if (effectivelyPassed)
            {
                continue;
            }

            MarkRemainingNotExecuted(steps, testCase, i + 1, capture);

            if (step.ExpectFailure)
            {
                // The declared-to-fail operation unexpectedly succeeded: a distinct classification
                // from an ordinary assertion failure, since it signals the oracle didn't behave as declared.
                return (false, FailureClassification.Unstable, "expected-failure-did-not-occur", steps);
            }

            var classification = isTimeout ? FailureClassification.Infrastructure : FailureClassification.Product;
            return (false, classification, stepDetail, steps);
        }

        return (true, null, null, steps);
    }

    private static void RecordStep(List<StepEvidence>? steps, int index, PipelineStep step, StepEvidenceOutcome outcome, TimeSpan duration, IOperation? operation, bool capture)
    {
        if (!capture || steps is null)
        {
            return;
        }

        EvidenceContribution? contribution = null;
        if (operation is IEvidenceEmittingOperation emitter)
        {
            contribution = emitter.DrainEvidence();
        }

        steps.Add(new StepEvidence(
            index,
            step.OperationName,
            outcome,
            duration,
            contribution?.Assertion,
            contribution?.Adapter));
    }

    private static void MarkRemainingNotExecuted(List<StepEvidence>? steps, TestCase testCase, int fromIndex, bool capture)
    {
        if (!capture || steps is null)
        {
            return;
        }

        for (var i = fromIndex; i < testCase.Pipeline.Count; i++)
        {
            steps.Add(new StepEvidence(i, testCase.Pipeline[i].OperationName, StepEvidenceOutcome.NotExecuted, TimeSpan.Zero));
        }
    }

    private static List<StepEvidence>? AllNotExecuted(TestCase testCase, bool capture)
    {
        if (!capture)
        {
            return null;
        }

        var steps = new List<StepEvidence>();
        MarkRemainingNotExecuted(steps, testCase, 0, capture: true);
        return steps;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyCaptures = new Dictionary<string, string>();

    private static async Task<(bool Succeeded, string? Detail, bool IsTimeout, IReadOnlyDictionary<string, string> Captures)> ExecuteStepWithRetryAsync(
        IOperation operation, CaseExecutionContext context, PipelineStep step, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var retry = step.EffectiveRetry;
        string? lastDetail = null;
        var lastTimeout = false;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            using var cts = retry.Timeout is { } timeout
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            cts?.CancelAfter(retry.Timeout!.Value);

            try
            {
                var result = await operation.ExecuteAsync(context, parameters, step.Captures, cts?.Token ?? cancellationToken);
                if (result.Succeeded)
                {
                    return (true, result.Detail, false, result.Captures ?? EmptyCaptures);
                }

                lastDetail = result.Detail;
                lastTimeout = false;
            }
            catch (OperationCanceledException) when (cts is not null && cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                lastDetail = "timeout";
                lastTimeout = true;
            }
        }

        return (false, lastDetail, lastTimeout, EmptyCaptures);
    }

    private async Task<CleanupStatus> RunCleanupAsync(TestCase testCase, CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (testCase.Cleanup.Count == 0)
        {
            return CleanupStatus.NotRun;
        }

        var allSucceeded = true;
        foreach (var declaration in testCase.Cleanup)
        {
            _cleanups.TryGet(declaration.OperationName, out var operation);
            var result = await operation!.ExecuteAsync(context, cancellationToken);
            if (!result.Succeeded)
            {
                allSucceeded = false;
            }
        }

        return allSucceeded ? CleanupStatus.AllSucceeded : CleanupStatus.SomeFailed;
    }

    private static bool VerifyFixture(FixtureReference fixture)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(fixture.Content)).ToLowerInvariant();
        return string.Equals(actualHash, fixture.ExpectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateReferences(TestCase testCase)
    {
        foreach (var declaration in testCase.Prerequisites)
        {
            if (!_prerequisites.TryGet(declaration.CheckName, out _))
            {
                throw new UnknownReferenceException($"Unknown prerequisite check '{declaration.CheckName}'");
            }
        }

        foreach (var step in testCase.Pipeline)
        {
            if (!_operations.TryGet(step.OperationName, out _))
            {
                throw new UnknownReferenceException($"Unknown operation '{step.OperationName}'");
            }
        }

        foreach (var declaration in testCase.Cleanup)
        {
            if (!_cleanups.TryGet(declaration.OperationName, out _))
            {
                throw new UnknownReferenceException($"Unknown cleanup operation '{declaration.OperationName}'");
            }
        }
    }

    private static CaseExecutionResult Result(
        TestCase testCase, Stopwatch stopwatch, bool passed, FailureClassification? classification, string? detail, CleanupStatus cleanupStatus,
        bool capture, IReadOnlyList<StepEvidence>? steps)
    {
        var report = Report(testCase, stopwatch, passed, classification, detail, cleanupStatus);
        var evidence = capture
            ? new RunEvidence(testCase.CaseId, testCase.Oracle.Locator, steps ?? Array.Empty<StepEvidence>())
            : null;
        return new CaseExecutionResult(report, evidence);
    }

    private static CaseReport Report(
        TestCase testCase, Stopwatch stopwatch, bool passed, FailureClassification? classification, string? detail, CleanupStatus cleanupStatus)
    {
        stopwatch.Stop();
        return new CaseReport(
            testCase.CaseId,
            testCase.Oracle,
            testCase.Fixture.ExpectedSha256,
            passed,
            passed ? null : classification,
            passed ? null : detail,
            cleanupStatus,
            stopwatch.Elapsed);
    }
}
