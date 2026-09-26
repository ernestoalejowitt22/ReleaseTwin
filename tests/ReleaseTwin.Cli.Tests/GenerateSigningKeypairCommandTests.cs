namespace ReleaseTwin.Cli.Tests;

/// <summary>evidence-integrity task 1.2: the `generate-signing-keypair` subcommand — dispatch
/// ordering, and that it actually prints a usable PEM keypair with the private key never persisted
/// anywhere by the command itself.</summary>
public class GenerateSigningKeypairCommandTests
{
    private static Dictionary<string, string?> Environment() => new();

    [Fact]
    public async Task PrintsAPrivateAndPublicKeyPem()
    {
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(["generate-signing-keypair"], Environment(), output);

        Assert.Equal(0, exit);
        var text = output.ToString();
        Assert.Contains("BEGIN PRIVATE KEY", text);
        Assert.Contains("BEGIN PUBLIC KEY", text);
    }

    [Fact]
    public async Task TheGeneratedPrivateKeyActuallySignsAndVerifies()
    {
        var output = new StringWriter();
        await CliEntrypoint.RunAsync(["generate-signing-keypair"], Environment(), output);
        var text = output.ToString();

        var privateKeyPem = ExtractPem(text, "PRIVATE KEY");
        var publicKeyPem = ExtractPem(text, "PUBLIC KEY");

        using var signer = System.Security.Cryptography.ECDsa.Create();
        signer.ImportFromPem(privateKeyPem);
        var signature = signer.SignData(
            System.Text.Encoding.UTF8.GetBytes("hello"),
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence);

        using var verifier = System.Security.Cryptography.ECDsa.Create();
        verifier.ImportFromPem(publicKeyPem);
        var verified = verifier.VerifyData(
            System.Text.Encoding.UTF8.GetBytes("hello"), signature,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(verified);
    }

    [Fact]
    public async Task TheVerbAppearsInUsage()
    {
        var output = new StringWriter();

        await CliEntrypoint.RunAsync(["--help"], Environment(), output);

        Assert.Contains("generate-signing-keypair", output.ToString());
    }

    [Fact]
    public async Task TheVerbIsDispatchedRatherThanTreatedAsACaseDirectory()
    {
        var output = new StringWriter();

        var exit = await CliEntrypoint.RunAsync(["generate-signing-keypair"], Environment(), output);

        Assert.Equal(0, exit);
        // A run fallthrough would look for a `generate-signing-keypair` cases directory and fail
        // differently; this command always succeeds regardless of the current directory's contents.
        Assert.DoesNotContain("cases", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPem(string text, string label)
    {
        var start = text.IndexOf($"-----BEGIN {label}-----", StringComparison.Ordinal);
        var end = text.IndexOf($"-----END {label}-----", StringComparison.Ordinal) + $"-----END {label}-----".Length;
        return text[start..end];
    }
}
