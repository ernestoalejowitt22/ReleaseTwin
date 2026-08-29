using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReleaseTwin.Cli.Evidence;

/// <summary>
/// evidence-capture: the one place a run's captured evidence is redacted, entirely inside the
/// customer's CLI, before anything is uploaded. Applies, in order: (1) an always-on built-in
/// denylist, (2) the case's own denylist, (3) the case's allowlist — which can never re-expose a
/// built-in-denied header or a resolved secret. Any rule that cannot be evaluated against a piece
/// of evidence drops that whole piece rather than risk uploading it unredacted (fail closed).
/// </summary>
public sealed class EvidenceRedactor
{
    public const string Mask = "«redacted»";

    public const string RedactionNote =
        "Redacted by your CLI before upload. Screenshots are best-effort-redacted.";

    private static readonly string[] BuiltInHeaderDrop =
    {
        "authorization", "proxy-authorization", "cookie", "set-cookie",
    };

    private static readonly Regex CredentialKeyName =
        new(@"(password|secret|token|api[_-]?key|authorization|credential|bearer)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IReadOnlyCollection<string> _secretValues;

    public EvidenceRedactor(IEnumerable<string> resolvedSecretValues)
    {
        _secretValues = resolvedSecretValues
            .Where(v => !string.IsNullOrEmpty(v) && v.Length >= 4)
            .Distinct()
            .ToList();
    }

    public RedactionResult Redact(RunEvidence primary, RunEvidence? knownBad, RunEvidence? knownGood, EvidenceRules rules)
    {
        var screenshots = new List<RedactedScreenshot>();
        var legs = new List<EvidenceLegDocument>();

        if (knownBad is not null || knownGood is not null)
        {
            if (knownBad is not null)
            {
                legs.Add(new EvidenceLegDocument("known-bad", RedactSteps(knownBad.Steps, rules, screenshots)));
            }

            if (knownGood is not null)
            {
                legs.Add(new EvidenceLegDocument("known-good", RedactSteps(knownGood.Steps, rules, screenshots)));
            }
        }
        else
        {
            legs.Add(new EvidenceLegDocument(null, RedactSteps(primary.Steps, rules, screenshots)));
        }

        var document = new EvidenceDocument(primary.CaseId, primary.OracleLocator, legs, RedactionNote);
        return new RedactionResult(document, screenshots);
    }

    private IReadOnlyList<EvidenceStepDocument> RedactSteps(IReadOnlyList<StepEvidence> steps, EvidenceRules rules, List<RedactedScreenshot> screenshots)
    {
        var result = new List<EvidenceStepDocument>(steps.Count);
        foreach (var step in steps)
        {
            EvidenceAssertionDocument? assertion = step.Assertion is null
                ? null
                : new EvidenceAssertionDocument(
                    step.Assertion.Expression,
                    RedactString(step.Assertion.Expected, rules),
                    RedactString(step.Assertion.Observed, rules));

            var (adapterJson, stepScreenshots) = RedactAdapterEvidence(step.AdapterEvidence, step.OperationName, rules, screenshots);

            result.Add(new EvidenceStepDocument(
                step.Index,
                step.OperationName,
                step.Outcome.ToString(),
                (long)step.Duration.TotalMilliseconds,
                assertion,
                adapterJson,
                stepScreenshots));
        }

        return result;
    }

    private (JToken? Json, IReadOnlyList<EvidenceScreenshotRef>? Screenshots) RedactAdapterEvidence(
        object? adapterEvidence, string operationName, EvidenceRules rules, List<RedactedScreenshot> screenshots)
    {
        if (adapterEvidence is null)
        {
            return (null, null);
        }

        // Pull screenshot handles out before serializing — they are files on disk, not JSON.
        var screenshotRefs = ExtractScreenshots(adapterEvidence, rules, screenshots);

        JToken token;
        try
        {
            token = JToken.FromObject(adapterEvidence, JsonSerializer.CreateDefault());
        }
        catch
        {
            return (null, screenshotRefs); // cannot even serialize it — drop it (fail closed)
        }

        try
        {
            // evidence-capture delta: a value typed into the UI is never uploaded verbatim. A
            // password-field value is masked in the adapter and marked protected — the allowlist
            // must not re-expose it; an ordinary ui.* `value` is masked here but stays re-includable.
            var valueIsProtected = operationName.StartsWith("ui.", StringComparison.Ordinal)
                && token is JObject uiObj && uiObj["ValueIsProtected"]?.Type == JTokenType.Boolean
                && (bool)uiObj["ValueIsProtected"]!;

            // Capture allowlisted originals before we start masking.
            var preserved = CaptureAllowlisted(token, rules);

            ApplyBuiltIn(token);
            ApplyCaseDenylist(token, rules);

            if (operationName.StartsWith("ui.", StringComparison.Ordinal))
            {
                MaskByPropertyName(token, "value");
            }

            RestoreAllowlisted(token, preserved, blockValueRestore: valueIsProtected);

            // Never leave a screenshot path in the JSON.
            StripScreenshotPaths(token);
            return (token, screenshotRefs);
        }
        catch
        {
            // A rule threw against this evidence — drop the whole adapter payload.
            return (null, screenshotRefs);
        }
    }

    // ---- built-in denylist ----

    private void ApplyBuiltIn(JToken token)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties().ToList())
                {
                    if (BuiltInHeaderDrop.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                        || CredentialKeyName.IsMatch(property.Name))
                    {
                        property.Value = Mask;
                        continue;
                    }

                    ApplyBuiltIn(property.Value);
                }

                break;
            case JArray array:
                foreach (var item in array)
                {
                    ApplyBuiltIn(item);
                }

                break;
            case JValue value when value.Type == JTokenType.String:
                value.Value = MaskSecrets((string?)value.Value);
                break;
        }
    }

    private string? MaskSecrets(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var masked = input!;
        foreach (var secret in _secretValues)
        {
            masked = masked.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return masked;
    }

    private string? RedactString(string? input, EvidenceRules rules)
    {
        var masked = MaskSecrets(input);
        if (masked is null)
        {
            return null;
        }

        foreach (var rule in rules.Redact.Where(r => r.Kind is EvidenceRedactKind.Field))
        {
            if (masked.Contains(rule.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Mask;
            }
        }

        return masked;
    }

    // ---- per-case denylist ----

    private static void ApplyCaseDenylist(JToken token, EvidenceRules rules)
    {
        foreach (var rule in rules.Redact)
        {
            switch (rule.Kind)
            {
                case EvidenceRedactKind.Header:
                case EvidenceRedactKind.Field:
                    MaskByPropertyName(token, rule.Value);
                    break;
                case EvidenceRedactKind.JsonPath:
                    // Throws on an invalid expression => caller drops the whole payload (fail closed).
                    foreach (var match in token.SelectTokens(rule.Value, errorWhenNoMatch: false).ToList())
                    {
                        ReplaceInPlace(match, Mask);
                    }

                    break;
                case EvidenceRedactKind.Selector:
                case EvidenceRedactKind.Region:
                    // Screenshot-only — handled during screenshot extraction.
                    break;
            }
        }
    }

    private static void MaskByPropertyName(JToken token, string name)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties().ToList())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        property.Value = Mask;
                    }
                    else
                    {
                        MaskByPropertyName(property.Value, name);
                    }
                }

                break;
            case JArray array:
                foreach (var item in array)
                {
                    MaskByPropertyName(item, name);
                }

                break;
        }
    }

    private static void ReplaceInPlace(JToken target, JToken replacement)
    {
        if (target.Parent is JProperty prop)
        {
            prop.Value = replacement;
        }
        else if (target.Parent is JArray arr)
        {
            var idx = arr.IndexOf(target);
            if (idx >= 0)
            {
                arr[idx] = replacement;
            }
        }
        else
        {
            target.Replace(replacement);
        }
    }

    // ---- per-case allowlist ----

    private Dictionary<string, JToken> CaptureAllowlisted(JToken token, EvidenceRules rules)
    {
        var preserved = new Dictionary<string, JToken>();
        foreach (var path in rules.CaptureAllow)
        {
            IEnumerable<JToken> matches;
            try
            {
                matches = token.SelectTokens(path, errorWhenNoMatch: false).ToList();
            }
            catch
            {
                continue; // an un-evaluable allowlist path just doesn't re-include anything
            }

            foreach (var match in matches)
            {
                if (match is JValue v && v.Type == JTokenType.String && ContainsSecret((string?)v.Value))
                {
                    continue; // never re-expose a resolved secret
                }

                preserved[match.Path] = match.DeepClone();
            }
        }

        return preserved;
    }

    private void RestoreAllowlisted(JToken root, Dictionary<string, JToken> preserved, bool blockValueRestore = false)
    {
        foreach (var (path, original) in preserved)
        {
            var current = root.SelectToken(path);
            if (current is null)
            {
                continue;
            }

            // The allowlist can re-include a value a built-in *key-name* rule dropped, but never a
            // hard-denied header (Authorization/Cookie/...) — those stay masked regardless.
            if (current.Parent is JProperty p && BuiltInHeaderDrop.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // A UI step's value that was typed into a password field stays masked regardless.
            if (blockValueRestore && current.Parent is JProperty vp && string.Equals(vp.Name, "value", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // ...and never a value that itself contains a resolved secret.
            if (original is JValue v && v.Type == JTokenType.String && ContainsSecret((string?)v.Value))
            {
                continue;
            }

            ReplaceInPlace(current, original);
        }
    }

    private bool ContainsSecret(string? value)
        => value is not null && _secretValues.Any(s => value.Contains(s, StringComparison.Ordinal));

    // ---- screenshots ----

    private IReadOnlyList<EvidenceScreenshotRef>? ExtractScreenshots(object adapterEvidence, EvidenceRules rules, List<RedactedScreenshot> sink)
    {
        var path = TryGetScreenshotPath(adapterEvidence);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var id = Guid.NewGuid().ToString("N");
        try
        {
            var regions = rules.Redact
                .Where(r => r.Kind is EvidenceRedactKind.Region)
                .Select(r => ParseRegion(r.Value))
                .Where(r => r is not null)
                .Select(r => r!.Value)
                .ToList();

            byte[] png;
            using (var image = Image.Load<Rgba32>(path))
            {
                foreach (var (rx, ry, rw, rh) in regions)
                {
                    var x0 = Math.Clamp((int)rx, 0, image.Width);
                    var y0 = Math.Clamp((int)ry, 0, image.Height);
                    var x1 = Math.Clamp((int)(rx + rw), 0, image.Width);
                    var y1 = Math.Clamp((int)(ry + rh), 0, image.Height);
                    for (var py = y0; py < y1; py++)
                    {
                        for (var px = x0; px < x1; px++)
                        {
                            image[px, py] = new Rgba32(0, 0, 0, 255);
                        }
                    }
                }

                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                png = ms.ToArray();
            }

            sink.Add(new RedactedScreenshot(id, png));
        }
        catch
        {
            return null; // could not process the image — drop it (fail closed)
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort temp cleanup */ }
        }

        return new[] { new EvidenceScreenshotRef(id, BestEffortRedacted: true) };
    }

    private static string? TryGetScreenshotPath(object adapterEvidence)
    {
        var property = adapterEvidence.GetType().GetProperty("ScreenshotPath");
        return property?.GetValue(adapterEvidence) as string;
    }

    private static void StripScreenshotPaths(JToken token)
    {
        MaskByPropertyName(token, "ScreenshotPath");
        MaskByPropertyName(token, "screenshotPath");
    }

    private static (int X, int Y, int W, int H)? ParseRegion(string spec)
    {
        var parts = spec.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y)
            || !int.TryParse(parts[2], out var w) || !int.TryParse(parts[3], out var h))
        {
            return null;
        }

        return (x, y, w, h);
    }
}
