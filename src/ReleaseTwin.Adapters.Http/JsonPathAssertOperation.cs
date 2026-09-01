using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Evaluates a JSONPath expression against the last http.request response body and compares it to
/// an expected value, both supplied as step parameters. design.md D3: uses Newtonsoft.Json's
/// SelectToken rather than a hand-rolled JSONPath evaluator.
/// </summary>
internal sealed class JsonPathAssertOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly object _evidenceLock = new();
    private AssertionDetail? _pending;

    public EvidenceContribution? DrainEvidence()
    {
        lock (_evidenceLock)
        {
            var detail = _pending;
            _pending = null;
            return detail is null ? null : new EvidenceContribution(Assertion: detail);
        }
    }

    private void Stash(AssertionDetail detail)
    {
        lock (_evidenceLock)
        {
            _pending = detail;
        }
    }

    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        lock (_evidenceLock)
        {
            _pending = null;
        }

        if (!parameters.TryGetValue("path", out var pathObj) || pathObj is not string path || string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult.Fail("http.assertJsonPath requires a 'path' parameter"));
        }

        if (!parameters.TryGetValue("expected", out var expected))
        {
            return Task.FromResult(OperationResult.Fail("http.assertJsonPath requires an 'expected' parameter"));
        }

        var expectedString = expected?.ToString();

        if (!context.AdapterState.TryGetValue("http.lastBody", out var bodyObj) || bodyObj is not string body)
        {
            Stash(new AssertionDetail(path, expectedString, null));
            return Task.FromResult(OperationResult.Fail("no prior http.request response to assert against"));
        }

        var match = JsonPathMatch.Evaluate(body, path, expectedString);
        if (match.Error is not null)
        {
            Stash(new AssertionDetail(path, expectedString, null));
            return Task.FromResult(OperationResult.Fail(match.Error));
        }

        Stash(new AssertionDetail(path, expectedString, match.Actual));

        return Task.FromResult(match.Matched
            ? OperationResult.Pass()
            : OperationResult.Fail($"expected '{expectedString}' but got '{match.Actual ?? "<missing>"}' at path '{path}'"));
    }
}
