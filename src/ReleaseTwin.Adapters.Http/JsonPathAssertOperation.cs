using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Evaluates a JSONPath expression against the last http.request response body and compares it to
/// an expected value, both supplied as step parameters. design.md D3: uses Newtonsoft.Json's
/// SelectToken rather than a hand-rolled JSONPath evaluator.
/// </summary>
internal sealed class JsonPathAssertOperation : IOperation
{
    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("path", out var pathObj) || pathObj is not string path || string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult.Fail("http.assertJsonPath requires a 'path' parameter"));
        }

        if (!parameters.TryGetValue("expected", out var expected))
        {
            return Task.FromResult(OperationResult.Fail("http.assertJsonPath requires an 'expected' parameter"));
        }

        if (!context.AdapterState.TryGetValue("http.lastBody", out var bodyObj) || bodyObj is not string body)
        {
            return Task.FromResult(OperationResult.Fail("no prior http.request response to assert against"));
        }

        JToken? token;
        try
        {
            token = JToken.Parse(body).SelectToken(path);
        }
        catch (JsonException ex)
        {
            return Task.FromResult(OperationResult.Fail($"response is not valid JSON: {ex.Message}"));
        }

        var actual = token?.ToString();
        var expectedString = expected?.ToString();

        return Task.FromResult(string.Equals(actual, expectedString, StringComparison.Ordinal)
            ? OperationResult.Pass()
            : OperationResult.Fail($"expected '{expectedString}' but got '{actual ?? "<missing>"}' at path '{path}'"));
    }
}
