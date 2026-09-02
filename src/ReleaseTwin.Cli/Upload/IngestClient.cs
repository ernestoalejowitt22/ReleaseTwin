using System.Net.Http.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Cli.Evidence;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Upload;

/// <summary>
/// design.md D1: mirrors the hosted platform's stable ingest contract as independently-defined DTOs
/// here — the CLI and the hosted API are separate solutions/deployments and deliberately don't share
/// a compiled type, matching how the ingest contract is meant to evolve independently of both sides'
/// internals.
///
/// evidence-capture: an optional, already-redacted <see cref="EvidenceDocument"/> may ride along with
/// a report. When it is absent the request body is byte-for-byte what it was before evidence existed.
/// </summary>
public sealed class IngestClient : IDisposable
{
    private readonly HttpClient _client;

    public IngestClient(string baseUrl, string apiToken, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    /// <summary>
    /// Uploads a case report. <see cref="IngestUploadResult.EvidenceAccepted"/> is true when no
    /// evidence was sent. <see cref="IngestUploadResult.ReportUrl"/> / <see cref="IngestUploadResult.RunUrl"/>
    /// are the dashboard links the hosted API returns (pr-annotation-evidence-link) — null against an
    /// older hosted API or an unparseable response.
    /// </summary>
    /// <param name="release">release-readiness-rollup: the uploaded case's optional <c>release</c> label. Null ⇒ the payload is the pre-release shape.</param>
    public async Task<IngestUploadResult> UploadCaseReportAsync(CaseReport report, RedactionResult? evidence, CancellationToken cancellationToken, string? release = null)
    {
        var payload = new
        {
            caseId = report.CaseId,
            oracleLocator = report.Oracle.Locator,
            fixtureSha256 = report.FixtureSha256,
            passed = report.Passed,
            classification = report.Classification?.ToString(),
            failureDetail = report.FailureDetail,
            cleanupStatus = report.CleanupStatus.ToString(),
            durationMs = (long)report.Duration.TotalMilliseconds,
            release,
        };

        return await SendAsync("/api/ingest/case-report", payload, evidence, cancellationToken);
    }

    public async Task<IngestUploadResult> UploadFlagProofReportAsync(FlagProofResult result, RedactionResult? evidence, CancellationToken cancellationToken, string? release = null)
    {
        var payload = new
        {
            caseId = result.CaseId,
            oracleLocator = result.Oracle.Locator,
            buildIdentity = result.BuildIdentity,
            outcome = result.Outcome.ToString(),
            knownBadLegPassed = result.KnownBadLeg?.Passed,
            knownGoodLegPassed = result.KnownGoodLeg?.Passed,
            release,
        };

        return await SendAsync("/api/ingest/flag-proof-report", payload, evidence, cancellationToken);
    }

    private async Task<IngestUploadResult> SendAsync(string path, object reportPayload, RedactionResult? evidence, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        if (evidence is null)
        {
            // No evidence: unchanged JSON POST, byte-for-byte as before this capability.
            response = await _client.PostAsJsonAsync(path, reportPayload, cancellationToken);
        }
        else if (evidence.Screenshots.Count == 0)
        {
            // The evidence document rides as an `evidence` property on the report object itself.
            var body = JObject.FromObject(reportPayload);
            body["evidence"] = JObject.FromObject(evidence.Document, CamelCase);
            response = await _client.PostAsync(path,
                new StringContent(body.ToString(), Encoding.UTF8, "application/json"),
                cancellationToken);
        }
        else
        {
            var reportJson = JObject.FromObject(reportPayload);
            reportJson["evidence"] = JObject.FromObject(evidence.Document, CamelCase);

            using var multipart = new MultipartFormDataContent
            {
                { new StringContent(reportJson.ToString(), Encoding.UTF8, "application/json"), "report" },
            };

            foreach (var screenshot in evidence.Screenshots)
            {
                var part = new ByteArrayContent(screenshot.PngBytes);
                part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                multipart.Add(part, $"screenshot:{screenshot.Id}", $"{screenshot.Id}.png");
            }

            response = await _client.PostAsync(path, multipart, cancellationToken);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();

            try
            {
                var ack = await response.Content.ReadFromJsonAsync<IngestAck>(cancellationToken: cancellationToken);
                return new IngestUploadResult(ack?.EvidenceAccepted ?? true, ack?.ReportUrl, ack?.RunUrl);
            }
            catch
            {
                return new IngestUploadResult(true, null, null);
            }
        }
    }

    private static readonly Newtonsoft.Json.JsonSerializer CamelCase = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
    {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
    });

    public void Dispose() => _client.Dispose();

    private sealed class IngestAck
    {
        public bool EvidenceAccepted { get; set; } = true;
        public string? ReportUrl { get; set; }
        public string? RunUrl { get; set; }
    }
}

/// <summary>
/// pr-annotation-evidence-link: the outcome of one report upload — whether an accompanying evidence
/// document was accepted, plus the dashboard links the hosted API returned (null against a hosted API
/// that predates this, or when the response could not be parsed).
/// </summary>
public readonly record struct IngestUploadResult(bool EvidenceAccepted, string? ReportUrl, string? RunUrl);
