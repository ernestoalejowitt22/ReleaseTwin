using System.Net;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class OutboundUrlValidatorTests
{
    private static IPAddress[] ResolveTo(params string[] ips) => ips.Select(IPAddress.Parse).ToArray();

    [Fact]
    public void PublicHttpsUrlIsAllowed()
    {
        Assert.True(OutboundUrlValidator.IsAllowed(
            "https://hooks.slack.com/services/T000/B000/xxx", out _, _ => ResolveTo("13.107.42.14")));
    }

    // security-hardening-pre-pilot D5: the overload yields the approved addresses for the caller to pin.
    [Fact]
    public void AllowedUrlYieldsItsApprovedAddresses()
    {
        var ok = OutboundUrlValidator.IsAllowed(
            "https://hooks.example.com/x", out _, out var approved, _ => ResolveTo("93.184.216.34", "93.184.216.35"));

        Assert.True(ok);
        Assert.Equal(new[] { IPAddress.Parse("93.184.216.34"), IPAddress.Parse("93.184.216.35") }, approved);
    }

    [Fact]
    public void RejectedUrlYieldsNoApprovedAddresses()
    {
        var ok = OutboundUrlValidator.IsAllowed(
            "https://evil.example.com/x", out _, out var approved, _ => ResolveTo("10.0.0.1"));

        Assert.False(ok);
        Assert.Empty(approved);
    }

    [Theory]
    [InlineData("http://hooks.slack.com/x")]          // not https
    [InlineData("ftp://example.com/x")]                // not https
    [InlineData("not a url")]
    [InlineData("")]
    public void NonHttpsOrMalformedIsRejected(string url)
    {
        Assert.False(OutboundUrlValidator.IsAllowed(url, out var reason, _ => ResolveTo("13.107.42.14")));
        Assert.NotEqual("", reason);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.9.9")]
    [InlineData("192.168.0.5")]
    [InlineData("169.254.169.254")]  // cloud metadata endpoint
    [InlineData("0.0.0.0")]
    public void HostResolvingToPrivateOrLoopbackIsRejected(string ip)
    {
        Assert.False(OutboundUrlValidator.IsAllowed("https://evil.example.com/hook", out var reason, _ => ResolveTo(ip)));
        Assert.Contains("non-public", reason);
    }

    [Fact]
    public void RejectedWhenAnyResolvedAddressIsPrivate()
    {
        Assert.False(OutboundUrlValidator.IsAllowed(
            "https://split.example.com/hook", out _, _ => ResolveTo("13.107.42.14", "10.0.0.1")));
    }

    [Fact]
    public void IPv6LoopbackAndUniqueLocalRejected()
    {
        Assert.False(OutboundUrlValidator.IsAllowed("https://[::1]/hook", out _));
        Assert.False(OutboundUrlValidator.IsAllowed("https://v6.example.com/hook", out _, _ => ResolveTo("fc00::1")));
    }

    [Fact]
    public void LiteralPublicIPv4IsAllowed()
    {
        Assert.True(OutboundUrlValidator.IsAllowed("https://13.107.42.14/hook", out _));
    }
}
