using System.Security.Cryptography;
using System.Text;
using ReleaseTwin.Cli.Evidence;
using ReleaseTwin.Cli.Upload;

namespace ReleaseTwin.Cli.Tests;

/// <summary>evidence-integrity task 1.1/1.3: the CLI's own digest/signature computation, matching
/// the hosted API's EvidenceManifestVerification.Compute algorithm exactly.</summary>
public class EvidenceManifestBuilderTests
{
    private static EvidenceDocument Doc() => new("CASE-1", "t/1", [], "Redacted.");

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public void ComputedDigestIsDeterministicAndMatchesTheCanonicalListingByHand()
    {
        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var screenshots = new[] { new RedactedScreenshot("b", [2]), new RedactedScreenshot("a", [1]) };
        var identity = "CASE-1\nfixture-hash";

        var manifest = EvidenceManifestBuilder.Build(documentJson, screenshots, identity, signingKeyPem: null);

        var expectedLines = new[]
        {
            $"identity:{Sha256Hex(Encoding.UTF8.GetBytes(identity))}",
            $"document:{Sha256Hex(Encoding.UTF8.GetBytes(documentJson))}",
            $"screenshot:a:{Sha256Hex([1])}",
            $"screenshot:b:{Sha256Hex([2])}",
        };
        var expectedBundleDigest = Sha256Hex(Encoding.UTF8.GetBytes(string.Join("\n", expectedLines)));

        Assert.Equal(expectedBundleDigest, manifest.BundleDigest);
        Assert.Equal("sha256-v1", manifest.Algorithm);
        Assert.Null(manifest.Signature);
    }

    [Fact]
    public void ScreenshotOrderDoesNotAffectTheDigest()
    {
        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var identity = "CASE-1\nfixture-hash";

        var inOrderA = EvidenceManifestBuilder.Build(documentJson, [new("a", [1]), new("b", [2])], identity, null);
        var inOrderB = EvidenceManifestBuilder.Build(documentJson, [new("b", [2]), new("a", [1])], identity, null);

        Assert.Equal(inOrderA.BundleDigest, inOrderB.BundleDigest);
    }

    [Fact]
    public void ArtifactDigestsAreKeyedByDocumentAndEachScreenshotId()
    {
        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var manifest = EvidenceManifestBuilder.Build(documentJson, [new RedactedScreenshot("shot-1", [9, 9])], "id", null);

        Assert.Equal(Sha256Hex(Encoding.UTF8.GetBytes(documentJson)), manifest.ArtifactDigests["document"]);
        Assert.Equal(Sha256Hex([9, 9]), manifest.ArtifactDigests["shot-1"]);
    }

    [Fact]
    public void NoSigningKeyProducesADigestOnlyManifest()
    {
        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var manifest = EvidenceManifestBuilder.Build(documentJson, [], "id", signingKeyPem: null);

        Assert.Null(manifest.Signature);
        Assert.NotEmpty(manifest.BundleDigest);
    }

    [Fact]
    public void ASignatureVerifiesAgainstTheMatchingPublicKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPem = ecdsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();

        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var manifest = EvidenceManifestBuilder.Build(documentJson, [], "id", privateKeyPem);

        Assert.NotNull(manifest.Signature);

        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(publicKeyPem);
        var verified = verifier.VerifyData(
            Encoding.UTF8.GetBytes(manifest.BundleDigest),
            Convert.FromBase64String(manifest.Signature!),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(verified);
    }

    [Fact]
    public void ASignatureDoesNotVerifyAgainstADifferentKey()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var documentJson = EvidenceManifestBuilder.BuildDocumentJson(Doc());
        var manifest = EvidenceManifestBuilder.Build(documentJson, [], "id", signingKey.ExportPkcs8PrivateKeyPem());

        var verified = otherKey.VerifyData(
            Encoding.UTF8.GetBytes(manifest.BundleDigest),
            Convert.FromBase64String(manifest.Signature!),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        Assert.False(verified);
    }
}
