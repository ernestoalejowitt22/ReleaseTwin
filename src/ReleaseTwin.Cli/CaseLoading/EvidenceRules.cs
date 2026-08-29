namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>
/// evidence-capture: the per-case redaction rules the CLI applies to captured evidence before any
/// upload. Empty when a case declares no <c>evidence:</c> block — the built-in denylist still runs.
/// </summary>
public sealed record EvidenceRules(
    IReadOnlyList<string> CaptureAllow,
    IReadOnlyList<EvidenceRedactRule> Redact)
{
    public static EvidenceRules None { get; } = new(Array.Empty<string>(), Array.Empty<EvidenceRedactRule>());

    public bool IsEmpty => CaptureAllow.Count == 0 && Redact.Count == 0;
}

public enum EvidenceRedactKind
{
    /// <summary>A request/response header name to drop.</summary>
    Header,

    /// <summary>A JSONPath expression whose matched value(s) are masked in every captured body.</summary>
    JsonPath,

    /// <summary>An object key name to mask wherever it appears in a captured body.</summary>
    Field,

    /// <summary>A UI selector whose screenshot region is masked (best-effort).</summary>
    Selector,

    /// <summary>A screenshot pixel region "x,y,w,h" to mask (best-effort).</summary>
    Region,
}

public sealed record EvidenceRedactRule(EvidenceRedactKind Kind, string Value);
