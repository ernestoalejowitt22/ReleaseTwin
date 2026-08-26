using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// Shared value-capture extraction for any HTTP-based operation's response (json field, header, or
/// cookie), so http.request and http.oauth2ClientCredentials capture the same way.
/// </summary>
internal static class HttpCaptureExtractor
{
    public static bool TryExtractAll(
        IReadOnlyList<CaptureDeclaration> captures,
        string responseBody,
        HttpResponseMessage response,
        out IReadOnlyDictionary<string, string> capturedValues,
        out string? error)
    {
        var values = new Dictionary<string, string>();
        foreach (var capture in captures)
        {
            if (!TryExtractCapture(capture.From, responseBody, response, out var value, out var captureError))
            {
                capturedValues = values;
                error = $"capture '{capture.Name}' failed: {captureError}";
                return false;
            }

            values[capture.Name] = value!;
        }

        capturedValues = values;
        error = null;
        return true;
    }

    /// <summary>
    /// A capture's `From` locator has the form `&lt;kind&gt;:&lt;locator&gt;`, e.g. `json:$.token`,
    /// `header:X-Auth-Token`, or `cookie:session`.
    /// </summary>
    private static bool TryExtractCapture(string from, string responseBody, HttpResponseMessage response, out string? value, out string? error)
    {
        var separatorIndex = from.IndexOf(':');
        if (separatorIndex < 0)
        {
            value = null;
            error = $"capture source '{from}' must be of the form 'json:<path>', 'header:<name>', or 'cookie:<name>'";
            return false;
        }

        var kind = from[..separatorIndex];
        var locator = from[(separatorIndex + 1)..];

        switch (kind)
        {
            case "json":
                return TryExtractJson(responseBody, locator, out value, out error);
            case "header":
                return TryExtractHeader(response, locator, out value, out error);
            case "cookie":
                return TryExtractCookie(response, locator, out value, out error);
            default:
                value = null;
                error = $"unknown capture source kind '{kind}' (expected 'json', 'header', or 'cookie')";
                return false;
        }
    }

    private static bool TryExtractJson(string responseBody, string path, out string? value, out string? error)
    {
        JToken? token;
        try
        {
            token = JToken.Parse(responseBody).SelectToken(path);
        }
        catch (JsonException ex)
        {
            value = null;
            error = $"response is not valid JSON: {ex.Message}";
            return false;
        }

        if (token is null)
        {
            value = null;
            error = $"json path '{path}' not found in response";
            return false;
        }

        value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
        error = null;
        return true;
    }

    private static bool TryExtractHeader(HttpResponseMessage response, string name, out string? value, out string? error)
    {
        if ((response.Headers.TryGetValues(name, out var headerValues) ||
             response.Content.Headers.TryGetValues(name, out headerValues)) &&
            headerValues.FirstOrDefault() is { } found)
        {
            value = found;
            error = null;
            return true;
        }

        value = null;
        error = $"header '{name}' not found in response";
        return false;
    }

    private static bool TryExtractCookie(HttpResponseMessage response, string name, out string? value, out string? error)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                var namePart = cookie.Split(';', 2)[0];
                var eq = namePart.IndexOf('=');
                if (eq > 0 && string.Equals(namePart[..eq].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    value = namePart[(eq + 1)..].Trim();
                    error = null;
                    return true;
                }
            }
        }

        value = null;
        error = $"cookie '{name}' not found in response";
        return false;
    }
}
