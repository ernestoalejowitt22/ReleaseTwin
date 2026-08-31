using System.Security.Cryptography;
using System.Text;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing: verifies a Polar webhook using the Standard Webhooks scheme Polar implements —
/// <c>webhook-id</c>, <c>webhook-timestamp</c>, and a space-separated <c>webhook-signature</c> list of
/// <c>v1,&lt;base64&gt;</c> entries over <c>"{id}.{timestamp}.{body}"</c> keyed by the (optionally
/// <c>whsec_</c>-prefixed, base64) signing secret. A missing or non-matching signature ⇒ reject, no
/// state change (billing spec).
/// </summary>
public static class BillingWebhookSignature
{
    public static bool Verify(string? secret, string? webhookId, string? webhookTimestamp, string? webhookSignatureHeader, string body)
    {
        if (string.IsNullOrWhiteSpace(secret)
            || string.IsNullOrWhiteSpace(webhookId)
            || string.IsNullOrWhiteSpace(webhookTimestamp)
            || string.IsNullOrWhiteSpace(webhookSignatureHeader))
        {
            return false;
        }

        byte[] key;
        try
        {
            var raw = secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret["whsec_".Length..] : secret;
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            // A non-base64 secret is used verbatim as UTF-8 bytes (Polar sandbox secrets are sometimes plain).
            key = Encoding.UTF8.GetBytes(secret);
        }

        var signedContent = $"{webhookId}.{webhookTimestamp}.{body}";
        using var hmac = new HMACSHA256(key);
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        foreach (var entry in webhookSignatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = entry.Contains(',') ? entry[(entry.IndexOf(',') + 1)..] : entry;
            var candidateBytes = Encoding.UTF8.GetBytes(candidate);
            if (CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes))
            {
                return true;
            }
        }

        return false;
    }
}
