using System.Net;
using System.Net.Sockets;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// run-notifications (design D6): a customer-supplied webhook URL must be a plain HTTPS URL that does
/// not resolve to a private, loopback, or link-local address — the notification worker POSTs to it
/// unauthenticated and must never be turned into an SSRF probe of internal infrastructure. Checked at
/// save time, and again at send time (DNS can change between the two).
/// </summary>
public static class OutboundUrlValidator
{
    /// <summary>
    /// True if <paramref name="url"/> is an absolute HTTPS URL whose host resolves only to public
    /// addresses. On false, <paramref name="reason"/> is a short human-readable explanation.
    /// <paramref name="resolve"/> defaults to <see cref="Dns.GetHostAddresses(string)"/>; tests inject a fake.
    /// </summary>
    public static bool IsAllowed(string? url, out string reason, Func<string, IPAddress[]>? resolve = null) =>
        IsAllowed(url, out reason, out _, resolve);

    /// <summary>
    /// As <see cref="IsAllowed(string?, out string, Func{string, IPAddress[]}?)"/>, but also yields the
    /// resolved, approved addresses. security-hardening-pre-pilot D5: the send-time caller pins its
    /// connection to one of these, so a name whose resolution changes between this check and the
    /// socket connect cannot redirect delivery to a private address.
    /// </summary>
    public static bool IsAllowed(string? url, out string reason, out IPAddress[] approvedAddresses, Func<string, IPAddress[]>? resolve = null)
    {
        reason = "";
        approvedAddresses = Array.Empty<IPAddress>();

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "not a valid absolute URL";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = "must be an https:// URL";
            return false;
        }

        if (uri.HostNameType is UriHostNameType.Unknown or UriHostNameType.Basic)
        {
            reason = "host is not a valid hostname or IP";
            return false;
        }

        IPAddress[] addresses;
        try
        {
            addresses = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                ? [IPAddress.Parse(uri.Host.Trim('[', ']'))]
                : (resolve ?? Dns.GetHostAddresses)(uri.IdnHost);
        }
        catch (Exception ex) when (ex is SocketException or FormatException or ArgumentException)
        {
            reason = "host could not be resolved";
            return false;
        }

        if (addresses.Length == 0)
        {
            reason = "host resolved to no addresses";
            return false;
        }

        foreach (var address in addresses)
        {
            if (IsDisallowed(address))
            {
                reason = $"host resolves to a non-public address ({address})";
                return false;
            }
        }

        approvedAddresses = addresses;
        return true;
    }

    private static bool IsDisallowed(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6Multicast || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return IsDisallowed(address.MapToIPv4());
            }

            var v6 = address.GetAddressBytes();
            // fc00::/7 unique-local
            if ((v6[0] & 0xFE) == 0xFC)
            {
                return true;
            }
            // ::1 handled by IsLoopback; :: (unspecified)
            return address.Equals(IPAddress.IPv6Any);
        }

        var b = address.GetAddressBytes();
        return b[0] == 10                                   // 10.0.0.0/8
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
            || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
            || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 link-local
            || b[0] == 127                                  // 127.0.0.0/8
            || b[0] == 0                                    // 0.0.0.0/8
            || b[0] >= 224;                                 // multicast + reserved
    }
}
