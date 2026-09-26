using System.Security.Cryptography;

namespace ReleaseTwin.Cli.Upload;

/// <summary>
/// evidence-integrity (task 1.2): `releasetwin generate-signing-keypair` — generates an ECDSA
/// P-256 keypair for evidence-manifest signing. The private key is printed once and never written
/// anywhere else by this command (no temp files, no logging) — the customer is responsible for
/// putting it in their own CI secret store, read back via `RELEASETWIN_SIGNING_KEY`. The public key
/// is printed for the customer to paste into the ReleaseTwin dashboard's project settings.
///
/// design.md - "Signing": the hosted platform never possesses, even transiently, anything capable
/// of producing a valid signature — this command is the one place a private key exists, and it
/// exists only in this process's memory and on this command's stdout.
/// </summary>
public static class GenerateSigningKeypairCommand
{
    public static Task<int> RunAsync(TextWriter output)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var privateKeyPem = ecdsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();

        output.WriteLine("Private key — store this in your own CI secret store as RELEASETWIN_SIGNING_KEY.");
        output.WriteLine("It is shown once and never written anywhere else by this command:");
        output.WriteLine();
        output.WriteLine(privateKeyPem);
        output.WriteLine("Public key — paste this into the project's \"Evidence verification key\" dashboard setting:");
        output.WriteLine();
        output.WriteLine(publicKeyPem);

        return Task.FromResult(0);
    }
}
