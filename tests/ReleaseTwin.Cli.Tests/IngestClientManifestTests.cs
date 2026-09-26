using System.Net;
using System.Net.Http.Json;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Cli.Evidence;
using ReleaseTwin.Cli.Upload;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Tests;

/// <summary>evidence-integrity task 1.4: the manifest actually appears on the wire, in both the
/// no-screenshots (JSON body) and with-screenshots (multipart) shapes, and its digest matches what
/// was actually sent — proving the CLI doesn't fall into the "hash one serialization, send another"
/// trap the hosted side's design.md warns about.</summary>
public class IngestClientManifestTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? CapturedBody { get; private set; }
        public List<(string Name, byte[] Bytes)> CapturedFiles { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                    if (name == "report")
                    {
                        CapturedBody = await part.ReadAsStringAsync(cancellationToken);
                    }
                    else if (name is not null)
                    {
                        CapturedFiles.Add((name, await part.ReadAsByteArrayAsync(cancellationToken)));
                    }
                }
            }
            else if (request.Content is not null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { evidenceAccepted = true, reportUrl = "https://x/1", runUrl = "https://x/run" }),
            };
        }
    }

    private static CaseReport SampleReport() => new(
        "CASE-1", new OracleReference("t/1"), "fixture-hash", true, null, null, CleanupStatus.AllSucceeded, TimeSpan.FromSeconds(1));

    private static EvidenceDocument SampleDocument() => new("CASE-1", "t/1", [], "Redacted.");

    [Fact]
    public async Task NoScreenshots_ManifestRidesOnTheJsonBodyAndMatchesTheSentDocument()
    {
        var handler = new CapturingHandler();
        using var client = new IngestClient("https://hosted.example", "tok", handler);
        var evidence = new RedactionResult(SampleDocument(), []);

        await client.UploadCaseReportAsync(SampleReport(), evidence, default);

        Assert.NotNull(handler.CapturedBody);
        var body = JObject.Parse(handler.CapturedBody!);
        var manifest = body["manifest"];
        Assert.NotNull(manifest);
        Assert.Equal("sha256-v1", manifest!["algorithm"]!.Value<string>());
        Assert.Null(manifest["signature"]);

        // The digest must match what the CLI would compute from the exact "evidence" text actually
        // sent — not a separately re-serialized copy of the same document.
        var sentEvidenceJson = body["evidence"]!.ToString(Newtonsoft.Json.Formatting.None);
        var recomputed = EvidenceManifestBuilder.Build(sentEvidenceJson, [], "CASE-1\nfixture-hash", null);
        Assert.Equal(recomputed.BundleDigest, manifest["bundleDigest"]!.Value<string>());
    }

    [Fact]
    public async Task WithScreenshots_ManifestRidesInTheMultipartReportPart()
    {
        var handler = new CapturingHandler();
        using var client = new IngestClient("https://hosted.example", "tok", handler);
        var evidence = new RedactionResult(SampleDocument(), [new RedactedScreenshot("shot-1", [1, 2, 3])]);

        await client.UploadCaseReportAsync(SampleReport(), evidence, default);

        Assert.NotNull(handler.CapturedBody);
        var manifest = JObject.Parse(handler.CapturedBody!)["manifest"];
        Assert.NotNull(manifest);
        Assert.Contains("shot-1", ((JObject)manifest!["artifactDigests"]!).Properties().Select(p => p.Name));
        Assert.Single(handler.CapturedFiles, f => f.Name == "screenshot:shot-1");
    }

    [Fact]
    public async Task NoEvidence_RequestCarriesNoManifest()
    {
        var handler = new CapturingHandler();
        using var client = new IngestClient("https://hosted.example", "tok", handler);

        await client.UploadCaseReportAsync(SampleReport(), evidence: null, default);

        Assert.NotNull(handler.CapturedBody);
        Assert.Null(JObject.Parse(handler.CapturedBody!)["manifest"]);
    }

    [Fact]
    public async Task ASigningKeyAddsAVerifiableSignature()
    {
        using var signingKey = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var privateKeyPem = signingKey.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = signingKey.ExportSubjectPublicKeyInfoPem();

        var handler = new CapturingHandler();
        using var client = new IngestClient("https://hosted.example", "tok", handler, privateKeyPem);
        var evidence = new RedactionResult(SampleDocument(), []);

        await client.UploadCaseReportAsync(SampleReport(), evidence, default);

        var manifest = JObject.Parse(handler.CapturedBody!)["manifest"]!;
        var signature = manifest["signature"]!.Value<string>()!;
        var bundleDigest = manifest["bundleDigest"]!.Value<string>()!;

        using var verifier = System.Security.Cryptography.ECDsa.Create();
        verifier.ImportFromPem(publicKeyPem);
        var verified = verifier.VerifyData(
            System.Text.Encoding.UTF8.GetBytes(bundleDigest),
            Convert.FromBase64String(signature),
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(verified);
    }

    [Fact]
    public async Task FlagProofReport_UsesCaseIdAndBuildIdentityForIdentityBinding()
    {
        var handler = new CapturingHandler();
        using var client = new IngestClient("https://hosted.example", "tok", handler);
        var result = new FlagProofResult("CASE-1", new OracleReference("t/1"), "build-42", FlagProofOutcome.Passed, null, null);
        var evidence = new RedactionResult(SampleDocument(), []);

        await client.UploadFlagProofReportAsync(result, evidence, default);

        var body = JObject.Parse(handler.CapturedBody!);
        var sentEvidenceJson = body["evidence"]!.ToString(Newtonsoft.Json.Formatting.None);
        var recomputed = EvidenceManifestBuilder.Build(sentEvidenceJson, [], "CASE-1\nbuild-42", null);
        Assert.Equal(recomputed.BundleDigest, body["manifest"]!["bundleDigest"]!.Value<string>());
    }
}
