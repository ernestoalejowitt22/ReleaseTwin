using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Issues an HTTP request entirely from step parameters (method, url, headers, body) and stores the
/// response for a later http.assertJsonPath step in the same case to assert against. Also supports
/// declaring captures from its own response (JSON field, header, or cookie), per value-capture, and
/// a username/password convenience that builds a Basic auth header automatically.
/// </summary>
internal sealed class HttpRequestOperation : IOperation, IEvidenceEmittingOperation
{
    private readonly HttpClient _client;
    private readonly object _evidenceLock = new();
    private HttpRequestEvidence? _pendingEvidence;

    public HttpRequestOperation(HttpClient client) => _client = client;

    public EvidenceContribution? DrainEvidence()
    {
        lock (_evidenceLock)
        {
            var evidence = _pendingEvidence;
            _pendingEvidence = null;
            return evidence is null ? null : new EvidenceContribution(Assertion: null, Adapter: evidence);
        }
    }

    private void StashEvidence(HttpRequestEvidence evidence)
    {
        lock (_evidenceLock)
        {
            _pendingEvidence = evidence;
        }
    }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        lock (_evidenceLock)
        {
            _pendingEvidence = null;
        }

        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
        {
            return OperationResult.Fail("http.request requires a 'url' parameter");
        }

        var method = parameters.TryGetValue("method", out var methodObj) && methodObj is string methodName
            ? new HttpMethod(methodName)
            : HttpMethod.Get;

        using var request = new HttpRequestMessage(method, url);

        var headerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidenceRequestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters.TryGetValue("headers", out var headersObj) && headersObj is IEnumerable<KeyValuePair<string, object?>> headers)
        {
            foreach (var header in headers)
            {
                var value = header.Value?.ToString();
                request.Headers.TryAddWithoutValidation(header.Key, value);
                headerNames.Add(header.Key);
                evidenceRequestHeaders[header.Key] = value ?? string.Empty;
            }
        }

        if (parameters.TryGetValue("username", out var usernameObj) && usernameObj is string username &&
            !headerNames.Contains("Authorization"))
        {
            parameters.TryGetValue("password", out var passwordObj);
            var password = passwordObj as string ?? string.Empty;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {encoded}");
            evidenceRequestHeaders["Authorization"] = $"Basic {encoded}";
        }

        string? requestBodyJson = null;
        if (parameters.TryGetValue("body", out var bodyObj) && bodyObj is not null)
        {
            requestBodyJson = bodyObj is string s ? s : JsonConvert.SerializeObject(bodyObj);
            request.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            context.AdapterState["http.lastStatusCode"] = (int)response.StatusCode;
            context.AdapterState["http.lastBody"] = responseBody;

            var (reqBody, reqTruncated) = HttpEvidence.Cap(requestBodyJson);
            var (resBody, resTruncated) = HttpEvidence.Cap(responseBody);
            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }
            foreach (var header in response.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            StashEvidence(new HttpRequestEvidence(
                method.Method,
                url,
                evidenceRequestHeaders,
                reqBody,
                reqTruncated,
                (int)response.StatusCode,
                responseHeaders,
                resBody,
                resTruncated,
                stopwatch.ElapsedMilliseconds));

            if (!response.IsSuccessStatusCode)
            {
                return OperationResult.Fail($"HTTP {(int)response.StatusCode}: {responseBody}");
            }

            if (captures.Count == 0)
            {
                return OperationResult.Pass($"{(int)response.StatusCode}");
            }

            if (!HttpCaptureExtractor.TryExtractAll(captures, responseBody, response, out var capturedValues, out var error))
            {
                return OperationResult.Fail(error);
            }

            return OperationResult.Pass($"{(int)response.StatusCode}", capturedValues);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            StashEvidence(new HttpRequestEvidence(
                method.Method, url, evidenceRequestHeaders, HttpEvidence.Cap(requestBodyJson).Body, false,
                0, new Dictionary<string, string>(), null, false, stopwatch.ElapsedMilliseconds));
            return OperationResult.Fail(ex.Message);
        }
    }
}
