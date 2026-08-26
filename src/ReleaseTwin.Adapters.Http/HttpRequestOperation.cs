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
internal sealed class HttpRequestOperation : IOperation
{
    private readonly HttpClient _client;

    public HttpRequestOperation(HttpClient client) => _client = client;

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
        {
            return OperationResult.Fail("http.request requires a 'url' parameter");
        }

        var method = parameters.TryGetValue("method", out var methodObj) && methodObj is string methodName
            ? new HttpMethod(methodName)
            : HttpMethod.Get;

        using var request = new HttpRequestMessage(method, url);

        var headerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (parameters.TryGetValue("headers", out var headersObj) && headersObj is IEnumerable<KeyValuePair<string, object?>> headers)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value?.ToString());
                headerNames.Add(header.Key);
            }
        }

        if (parameters.TryGetValue("username", out var usernameObj) && usernameObj is string username &&
            !headerNames.Contains("Authorization"))
        {
            parameters.TryGetValue("password", out var passwordObj);
            var password = passwordObj as string ?? string.Empty;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {encoded}");
        }

        if (parameters.TryGetValue("body", out var bodyObj) && bodyObj is not null)
        {
            var bodyJson = bodyObj is string s ? s : JsonConvert.SerializeObject(bodyObj);
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            context.AdapterState["http.lastStatusCode"] = (int)response.StatusCode;
            context.AdapterState["http.lastBody"] = responseBody;

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
            return OperationResult.Fail(ex.Message);
        }
    }
}
