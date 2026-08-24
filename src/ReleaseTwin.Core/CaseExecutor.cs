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

    public async Task<CaseReport> ExecuteAsync(TestCase testCase, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new CaseExecutionContext { Case = testCase };

        var resourceLock = testCase.ResourceKey is null
            ? null
            : await _resourceSerializer.AcquireAsync(testCase.ResourceKey, cancellationToken);

        try
        {
            foreach (var capability in testCase.RequiredCapabilities)
            {
                if (!_capabilities.IsAvailable(capability.Name))
                {
                    return Report(testCase, stopwatch, passed: false, FailureClassification.Infrastructure, $"missing-capability:{capability.Name}", CleanupStatus.NotRun);
                }
            }

            // Required capabilities must be confirmed available before validating references: a case
            // whose gated capability isn't installed should report missing-capability, not crash on an
            // unknown operation/prerequisite/cleanup name that capability would have explained.
            ValidateReferences(testCase);

            if (!VerifyFixture(testCase.Fixture))
            {
                return Report(testCase, stopwatch, passed: false, FailureClassification.Infrastructure, "fixture-integrity-mismatch", CleanupStatus.NotRun);
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
                return Report(testCase, stopwatch, passed: false, prerequisiteClassification, result.Detail, haltedCleanupStatus);
            }

            var (passed, classification, detail) = await RunPipelineAsync(testCase, context, cancellationToken);
            var cleanupStatus = await RunCleanupAsync(testCase, context, cancellationToken);
            return Report(testCase, stopwatch, passed, classification, detail, cleanupStatus);
        }
        finally
        {
            resourceLock?.Dispose();
        }
    }

    private async Task<(bool Passed, FailureClassification? Classification, string? Detail)> RunPipelineAsync(
        TestCase testCase, CaseExecutionContext context, CancellationToken cancellationToken)
    {
        foreach (var step in testCase.Pipeline)
        {
            _operations.TryGet(step.OperationName, out var operation);
            var (succeeded, stepDetail, isTimeout) = await ExecuteStepWithRetryAsync(operation!, context, step, cancellationToken);

            var effectivelyPassed = step.ExpectFailure ? !succeeded : succeeded;
            if (effectivelyPassed)
            {
                continue;
            }

            if (step.ExpectFailure)
            {
                // The declared-to-fail operation unexpectedly succeeded: a distinct classification
                // from an ordinary assertion failure, since it signals the oracle didn't behave as declared.
                return (false, FailureClassification.Unstable, "expected-failure-did-not-occur");
            }

            var classification = isTimeout ? FailureClassification.Infrastructure : FailureClassification.Product;
            return (false, classification, stepDetail);
        }

        return (true, null, null);
    }

    private static async Task<(bool Succeeded, string? Detail, bool IsTimeout)> ExecuteStepWithRetryAsync(
        IOperation operation, CaseExecutionContext context, PipelineStep step, CancellationToken cancellationToken)
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
                var result = await operation.ExecuteAsync(context, step.Parameters, cts?.Token ?? cancellationToken);
                if (result.Succeeded)
                {
                    return (true, result.Detail, false);
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

        return (false, lastDetail, lastTimeout);
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
