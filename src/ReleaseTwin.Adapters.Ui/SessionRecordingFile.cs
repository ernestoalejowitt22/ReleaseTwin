namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// local-evidence-viewer (design D4): the one place that knows how a case id becomes a recording's
/// filename. The recorder writes <c>&lt;video-dir&gt;/&lt;sanitized-case-id&gt;.webm</c> and the
/// viewer looks a recording up by the same name, so the rule has to be shared rather than restated —
/// a duplicated copy would desynchronize silently the first time either side changed.
/// </summary>
public static class SessionRecordingFile
{
    public const string Extension = ".webm";

    /// <summary>
    /// Maps every character outside <c>[A-Za-z0-9-_.]</c> to <c>-</c>, so a case id is safe as a
    /// filename on every filesystem. Two case ids can collide (<c>A/B</c> and <c>A-B</c> both become
    /// <c>A-B</c>); that is pre-existing in the recorder and not resolved here.
    /// </summary>
    public static string Sanitize(string caseId) =>
        new(caseId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());

    /// <summary>The recording path a case would have been written to under <paramref name="videoDir"/>.</summary>
    public static string PathFor(string videoDir, string caseId) =>
        Path.Combine(videoDir, Sanitize(caseId) + Extension);
}
