using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Cli.Evidence;

namespace ReleaseTwin.Cli.Upload;

/// <summary>
/// evidence-integrity (tasks 1.1/1.3 of that OpenSpec change, which lives in the releasetwin-platform
/// repo — this repo, ReleaseTwin, is the CLI/AGPL side of it): computes the same canonical digest the
/// hosted API's <c>EvidenceManifestVerification.Compute</c> recomputes on ingest, and optionally signs
/// it with a customer-held private key.
///
/// The one rule that matters more than any other here: the document digest MUST be computed over the
/// exact bytes that end up on the wire as the "evidence" JSON value, not a separately re-serialized
/// copy. The hosted side hashes <c>JsonElement.GetRawText()</c> — the literal substring it received.
/// If this CLI computed the digest from one serialization and embedded a different one (even
/// differing only in whitespace), every single upload would show as a false mismatch. <see cref="BuildDocumentJson"/>
/// is the one place that JSON text is produced, and both <see cref="IngestClient"/> (for the wire
/// body) and this class (for the digest) must use its exact output — see <see cref="IngestClient"/>'s
/// use of <see cref="Newtonsoft.Json.Linq.JRaw"/> to embed it verbatim.
/// </summary>
public static class EvidenceManifestBuilder
{
    /// <summary>Serializes the evidence document to the exact, compact JSON text that will be
    /// embedded verbatim on the wire (via <see cref="JRaw"/>) and hashed for the manifest — the same
    /// camelCase contract <see cref="IngestClient"/> already used.</summary>
    public static string BuildDocumentJson(EvidenceDocument document) =>
        JObject.FromObject(document, CamelCase).ToString(Newtonsoft.Json.Formatting.None);

    private static readonly Newtonsoft.Json.JsonSerializer CamelCase = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
    {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
    });

    /// <summary>The manifest's own wire shape — camelCase-bound by the hosted API's
    /// <c>JsonSerializerDefaults.Web</c>.</summary>
    public sealed class Manifest
    {
        public string Algorithm { get; set; } = "sha256-v1";
        public string BundleDigest { get; set; } = "";
        public IReadOnlyDictionary<string, string> ArtifactDigests { get; set; } = new Dictionary<string, string>();
        public string? Signature { get; set; }
    }

    /// <param name="documentJson">Must be exactly <see cref="BuildDocumentJson"/>'s output for this
    /// same document — see the pitfall documented on this class.</param>
    /// <param name="identity">design.md - "Identity binding": <c>caseId + "\n" + fixtureSha256</c>
    /// for a case report, or <c>caseId + "\n" + buildIdentity</c> for a flag-proof report (which has
    /// no fixture hash) — must match the hosted side's <c>IngestService</c> exactly.</param>
    /// <param name="signingKeyPem">A PEM-encoded ECDSA P-256 private key, or null to produce an
    /// unsigned (still valid) manifest.</param>
    public static Manifest Build(
        string documentJson,
        IReadOnlyList<RedactedScreenshot> screenshots,
        string identity,
        string? signingKeyPem)
    {
        var artifactDigests = new Dictionary<string, string>();
        var lines = new List<string> { $"identity:{Sha256Hex(Encoding.UTF8.GetBytes(identity))}" };

        var documentDigest = Sha256Hex(Encoding.UTF8.GetBytes(documentJson));
        artifactDigests["document"] = documentDigest;
        lines.Add($"document:{documentDigest}");

        foreach (var screenshot in screenshots.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            var digest = Sha256Hex(screenshot.PngBytes);
            artifactDigests[screenshot.Id] = digest;
            lines.Add($"screenshot:{screenshot.Id}:{digest}");
        }

        var bundleDigest = Sha256Hex(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

        string? signature = null;
        if (signingKeyPem is not null)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(signingKeyPem);
            var signatureBytes = ecdsa.SignData(
                Encoding.UTF8.GetBytes(bundleDigest), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            signature = Convert.ToBase64String(signatureBytes);
        }

        return new Manifest
        {
            Algorithm = "sha256-v1",
            BundleDigest = bundleDigest,
            ArtifactDigests = artifactDigests,
            Signature = signature,
        };
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
