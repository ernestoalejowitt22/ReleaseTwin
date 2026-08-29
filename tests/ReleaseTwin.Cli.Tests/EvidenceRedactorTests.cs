using Newtonsoft.Json.Linq;
using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Cli.Evidence;
using ReleaseTwin.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReleaseTwin.Cli.Tests;

public class EvidenceRedactorTests
{
    private static RunEvidence OneStep(object adapterEvidence, AssertionDetail? assertion = null) =>
        new("CASE-1", "t/1", new[]
        {
            new StepEvidence(0, "http.request", StepEvidenceOutcome.Passed, TimeSpan.FromMilliseconds(5), assertion, adapterEvidence),
        });

    private static JObject AdapterJson(RedactionResult result) =>
        (JObject)result.Document.Legs[0].Steps[0].Adapter!;

    [Fact]
    public void BuiltIn_StripsAuthorizationAndCookieHeaders()
    {
        var evidence = OneStep(new
        {
            requestHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer abc", ["Cookie"] = "sid=1", ["Accept"] = "application/json" },
        });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, EvidenceRules.None);

        var headers = (JObject)AdapterJson(result)["requestHeaders"]!;
        Assert.Equal(EvidenceRedactor.Mask, (string?)headers["Authorization"]);
        Assert.Equal(EvidenceRedactor.Mask, (string?)headers["Cookie"]);
        Assert.Equal("application/json", (string?)headers["Accept"]);
    }

    [Fact]
    public void BuiltIn_StripsCredentialShapedKeys()
    {
        var evidence = OneStep(new { responseBody = "{}", apiKey = "sk-123", nested = new { password = "hunter2" } });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, EvidenceRules.None);
        var json = AdapterJson(result);

        Assert.Equal(EvidenceRedactor.Mask, (string?)json["apiKey"]);
        Assert.Equal(EvidenceRedactor.Mask, (string?)json["nested"]!["password"]);
    }

    [Fact]
    public void BuiltIn_MasksResolvedSecretSubstringInBodies()
    {
        var evidence = OneStep(new { responseBody = "{\"token\":\"SUPERSECRETVALUE\",\"ok\":true}" });

        var result = new EvidenceRedactor(new[] { "SUPERSECRETVALUE" }).Redact(evidence, null, null, EvidenceRules.None);

        Assert.DoesNotContain("SUPERSECRETVALUE", AdapterJson(result).ToString());
    }

    [Fact]
    public void CaseDenylist_JsonPath_MasksMatchedValue()
    {
        var evidence = OneStep(new { responseBody = new { customer = new { email = "a@b.com" }, id = 7 } });
        var rules = new EvidenceRules(Array.Empty<string>(), new[] { new EvidenceRedactRule(EvidenceRedactKind.JsonPath, "$.responseBody.customer.email") });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, rules);
        var json = AdapterJson(result);

        Assert.Equal(EvidenceRedactor.Mask, (string?)json["responseBody"]!["customer"]!["email"]);
        Assert.Equal(7, (int?)json["responseBody"]!["id"]);
    }

    [Fact]
    public void CaseDenylist_Field_MasksByName()
    {
        var evidence = OneStep(new { responseBody = new { ssn = "111-22-3333", ok = true } });
        var rules = new EvidenceRules(Array.Empty<string>(), new[] { new EvidenceRedactRule(EvidenceRedactKind.Field, "ssn") });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, rules);
        Assert.Equal(EvidenceRedactor.Mask, (string?)AdapterJson(result)["responseBody"]!["ssn"]);
    }

    [Fact]
    public void Allowlist_ReIncludesKeyNameDroppedField_ButNotAuthHeaderOrSecret()
    {
        var evidence = OneStep(new
        {
            requestHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer abc" },
            responseBody = new { access_token_label = "friendly-name", secretThing = "KEEPHIDDEN" },
        });
        // "access_token_label" matches the credential key regex (token) -> built-in would mask it.
        var rules = new EvidenceRules(
            new[] { "$.responseBody.access_token_label", "$.requestHeaders.Authorization", "$.responseBody.secretThing" },
            Array.Empty<EvidenceRedactRule>());

        var result = new EvidenceRedactor(new[] { "KEEPHIDDEN" }).Redact(evidence, null, null, rules);
        var json = AdapterJson(result);

        Assert.Equal("friendly-name", (string?)json["responseBody"]!["access_token_label"]);
        Assert.Equal(EvidenceRedactor.Mask, (string?)json["requestHeaders"]!["Authorization"]);
        Assert.Equal(EvidenceRedactor.Mask, (string?)json["responseBody"]!["secretThing"]);
    }

    [Fact]
    public void UnevaluableJsonPathRule_DropsWholeAdapterPayload_FailClosed()
    {
        var evidence = OneStep(new { responseBody = new { a = 1 } });
        var rules = new EvidenceRules(Array.Empty<string>(), new[] { new EvidenceRedactRule(EvidenceRedactKind.JsonPath, "$.[[[bad") });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, rules);
        Assert.Null(result.Document.Legs[0].Steps[0].Adapter);
    }

    [Fact]
    public void Screenshot_RegionIsMasked_AndLabelledBestEffort()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rt-test-{Guid.NewGuid():N}.png");
        using (var img = new Image<Rgba32>(20, 20, new Rgba32(255, 255, 255, 255)))
        {
            img.SaveAsPng(path);
        }

        var evidence = OneStep(new UiStepEvidenceLike { Action = "ui.navigate", ScreenshotPath = path });
        var rules = new EvidenceRules(Array.Empty<string>(), new[] { new EvidenceRedactRule(EvidenceRedactKind.Region, "0,0,10,10") });

        var result = new EvidenceRedactor(Array.Empty<string>()).Redact(evidence, null, null, rules);

        var shot = Assert.Single(result.Document.Legs[0].Steps[0].Screenshots!);
        Assert.True(shot.BestEffortRedacted);
        var blob = Assert.Single(result.Screenshots);
        using var masked = Image.Load<Rgba32>(blob.PngBytes);
        Assert.Equal(new Rgba32(0, 0, 0, 255), masked[2, 2]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), masked[15, 15]);
        Assert.False(File.Exists(path)); // temp cleaned up
    }

    private sealed class UiStepEvidenceLike
    {
        public string Action { get; set; } = "";
        public string? ScreenshotPath { get; set; }
    }
}
