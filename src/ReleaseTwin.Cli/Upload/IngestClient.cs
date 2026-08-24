using System.Net.Http.Json;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Upload;

/// <summary>
/// design.md D1: mirrors the hosted platform's stable ingest contract as independently-defined DTOs
/// here — the CLI and the hosted API are separate solutions/deployments and deliberately don't share
/// a compiled type, matching how the ingest contract is meant to evolve independently of both sides'
/// internals.
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

    public async Task UploadCaseReportAsync(CaseReport report, CancellationToken cancellationToken)
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
        };

        using var response = await _client.PostAsJsonAsync("/api/ingest/case-report", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadFlagProofReportAsync(FlagProofResult result, CancellationToken cancellationToken)
    {
        var payload = new
        {
            caseId = result.CaseId,
            oracleLocator = result.Oracle.Locator,
            buildIdentity = result.BuildIdentity,
            outcome = result.Outcome.ToString(),
            knownBadLegPassed = result.KnownBadLeg?.Passed,
            knownGoodLegPassed = result.KnownGoodLeg?.Passed,
        };

        using var response = await _client.PostAsJsonAsync("/api/ingest/flag-proof-report", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _client.Dispose();
}
