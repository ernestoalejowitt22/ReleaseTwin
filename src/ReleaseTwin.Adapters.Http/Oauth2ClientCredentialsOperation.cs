using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Performs a standard OAuth2 client-credentials grant against a token endpoint (RFC 6749 §4.4) so
/// a case doesn't have to hand-assemble the exchange. Captures from the token response the same way
/// http.request does — declare `capture: - name: token, from: json:$.access_token` on the step.
/// </summary>
internal sealed class Oauth2ClientCredentialsOperation : IOperation
{
    private readonly HttpClient _client;

    public Oauth2ClientCredentialsOperation(HttpClient client) => _client = client;

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("tokenUrl", out var tokenUrlObj) || tokenUrlObj is not string tokenUrl || string.IsNullOrWhiteSpace(tokenUrl))
        {
            return OperationResult.Fail("http.oauth2ClientCredentials requires a 'tokenUrl' parameter");
        }

        if (!parameters.TryGetValue("clientId", out var clientIdObj) || clientIdObj is not string clientId || string.IsNullOrWhiteSpace(clientId))
        {
            return OperationResult.Fail("http.oauth2ClientCredentials requires a 'clientId' parameter");
        }

        if (!parameters.TryGetValue("clientSecret", out var clientSecretObj) || clientSecretObj is not string clientSecret || string.IsNullOrWhiteSpace(clientSecret))
        {
            return OperationResult.Fail("http.oauth2ClientCredentials requires a 'clientSecret' parameter");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };

        if (parameters.TryGetValue("scope", out var scopeObj) && scopeObj is string scope && !string.IsNullOrWhiteSpace(scope))
        {
            form["scope"] = scope;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) { Content = new FormUrlEncodedContent(form) };

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
