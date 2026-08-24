using System.Net.Http.Headers;
using Newtonsoft.Json;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Issues an HTTP request entirely from step parameters (method, url, headers, body) and stores the
/// response for a later http.assertJsonPath step in the same case to assert against.
/// </summary>
internal sealed class HttpRequestOperation : IOperation
{
    private readonly HttpClient _client;

    public HttpRequestOperation(HttpClient client) => _client = client;

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
        {
            return OperationResult.Fail("http.request requires a 'url' parameter");
        }

        var method = parameters.TryGetValue("method", out var methodObj) && methodObj is string methodName
            ? new HttpMethod(methodName)
            : HttpMethod.Get;

        using var request = new HttpRequestMessage(method, url);

        if (parameters.TryGetValue("headers", out var headersObj) && headersObj is IEnumerable<KeyValuePair<string, object?>> headers)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value?.ToString());
            }
        }

        if (parameters.TryGetValue("body", out var bodyObj) && bodyObj is not null)
        {
            var bodyJson = bodyObj is string s ? s : JsonConvert.SerializeObject(bodyObj);
            request.Content = new StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            context.AdapterState["http.lastStatusCode"] = (int)response.StatusCode;
            context.AdapterState["http.lastBody"] = responseBody;

            return response.IsSuccessStatusCode
                ? OperationResult.Pass($"{(int)response.StatusCode}")
                : OperationResult.Fail($"HTTP {(int)response.StatusCode}: {responseBody}");
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
