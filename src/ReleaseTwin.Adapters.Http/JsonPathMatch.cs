using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// The shared JSONPath compare core used by both <see cref="JsonPathAssertOperation"/> and the
/// flag-proof read-back in <see cref="HttpFeatureStateController"/>: parse a response body, select a
/// token, stringify the scalar, and compare it ordinally to an expected string.
/// </summary>
internal static class JsonPathMatch
{
    internal readonly record struct Result(bool Matched, string? Actual, string? Error);

    /// <summary>
    /// Evaluates <paramref name="path"/> against <paramref name="body"/> and compares the selected
    /// token's string form to <paramref name="expected"/>. A body that is not valid JSON yields
    /// <see cref="Result.Error"/> set and <see cref="Result.Matched"/> false.
    /// </summary>
    internal static Result Evaluate(string body, string path, string? expected)
    {
        JToken? token;
        try
        {
            token = JToken.Parse(body).SelectToken(path);
        }
        catch (JsonException ex)
        {
            return new Result(false, null, $"response is not valid JSON: {ex.Message}");
        }

        // A JSON boolean stringifies to "True"/"False" via Newtonsoft; normalise to "true"/"false"
        // so an `expected` of "true" (and the flag-proof `{{enabled}}` token) matches naturally.
        var actual = token?.Type == JTokenType.Boolean
            ? ((bool)token! ? "true" : "false")
            : token?.ToString();
        return new Result(string.Equals(actual, expected, StringComparison.Ordinal), actual, null);
    }
}
