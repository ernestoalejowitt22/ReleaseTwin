namespace ReleaseTwin.Adapters.Http;

/// <summary>
/// evidence-capture: the adapter-defined evidence shape `http.request` attaches to a step. Carried
/// opaquely by the core; redacted by the CLI before any upload. Bodies are truncated at
/// <see cref="HttpEvidence.BodyCapBytes"/> before they ever leave the operation.
/// </summary>
public sealed record HttpRequestEvidence(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> RequestHeaders,
    string? RequestBody,
    bool RequestBodyTruncated,
    int StatusCode,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string? ResponseBody,
    bool ResponseBodyTruncated,
    long ElapsedMs);

public static class HttpEvidence
{
    /// <summary>Default per-body truncation cap applied in the operation, well under the ingest document cap.</summary>
    public const int BodyCapBytes = 32 * 1024;

    public static (string? Body, bool Truncated) Cap(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return (body, false);
        }

        return body!.Length <= BodyCapBytes
            ? (body, false)
            : (body[..BodyCapBytes], true);
    }
}
