using System.Text;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>Where a case's session recording comes from, or why it is not there.</summary>
public readonly record struct VideoLink(string? Src, string? MimeType, string? Note)
{
    public static readonly VideoLink None = new(null, null, null);
}

/// <summary>
/// local-evidence-viewer (design D1): the one thing that differs between the served report and the
/// exported file. The renderer emits identical HTML for both and asks this for every asset
/// reference, so the two reports cannot disagree about anything else.
/// </summary>
public abstract class EvidenceAssetLinks
{
    /// <summary>Markup for the document head — a stylesheet link, or the stylesheet itself.</summary>
    public abstract string Head();

    /// <summary>Markup for the end of the body — a script tag, or the script itself.</summary>
    public abstract string BodyEnd();

    /// <summary>The <c>src</c> for one screenshot, or null when its bytes could not be read.</summary>
    public abstract string? Screenshot(EvidenceCaseView evidenceCase, EvidenceScreenshotFile file);

    public abstract VideoLink Video(EvidenceCaseView evidenceCase);

    public static string CasePath(EvidenceCaseView evidenceCase) => Uri.EscapeDataString(evidenceCase.DirectoryName);
}

/// <summary>Assets fetched from the local server — screenshots and video stream from disk.</summary>
public sealed class ServedAssetLinks : EvidenceAssetLinks
{
    public const string StylesheetPath = "/assets/viewer.css";
    public const string ScriptPath = "/assets/viewer.js";

    public override string Head() => $"""<link rel="stylesheet" href="{StylesheetPath}">""";

    public override string BodyEnd() => $"""<script src="{ScriptPath}"></script>""";

    public override string Screenshot(EvidenceCaseView evidenceCase, EvidenceScreenshotFile file) =>
        $"/case/{CasePath(evidenceCase)}/screenshot/{Uri.EscapeDataString(file.Id)}.png";

    public override VideoLink Video(EvidenceCaseView evidenceCase) =>
        evidenceCase.VideoPath is null
            ? VideoLink.None
            : new VideoLink($"/case/{CasePath(evidenceCase)}/video.webm", "video/webm", null);
}

/// <summary>
/// Assets inlined as <c>data:</c> URIs for the single-file export. Screenshots are embedded
/// unconditionally; a session recording only when it is under <see cref="VideoByteCap"/> (design D5).
/// </summary>
public sealed class InlinedAssetLinks : EvidenceAssetLinks
{
    /// <summary>
    /// 24 MiB. Base64 inflates by a third, so the cap keeps an embedded recording under ~32 MB of
    /// markup — still attachable to a pull request, which is what the export exists for. Raising or
    /// lowering it changes no contract; the report says when a recording was left out.
    /// </summary>
    public const long VideoByteCap = 24L * 1024 * 1024;

    public override string Head() => $"<style>\n{ViewerAssets.Css}\n</style>";

    public override string BodyEnd() => $"<script>\n{ViewerAssets.Js}\n</script>";

    public override string? Screenshot(EvidenceCaseView evidenceCase, EvidenceScreenshotFile file) =>
        TryReadAllBytes(file.Path) is { } bytes ? DataUri("image/png", bytes) : null;

    public override VideoLink Video(EvidenceCaseView evidenceCase)
    {
        if (evidenceCase.VideoPath is not { } path)
        {
            return VideoLink.None;
        }

        long length;
        try
        {
            length = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new VideoLink(null, null, $"Session recording at {path} could not be read, so it is not in this file.");
        }

        if (length > VideoByteCap)
        {
            return new VideoLink(
                null,
                null,
                $"Session recording omitted from this file: {length / (1024 * 1024)} MB exceeds the {VideoByteCap / (1024 * 1024)} MB embed limit. It is at {path}.");
        }

        return TryReadAllBytes(path) is { } bytes
            ? new VideoLink(DataUri("video/webm", bytes), "video/webm", null)
            : new VideoLink(null, null, $"Session recording at {path} could not be read, so it is not in this file.");
    }

    private static string DataUri(string mimeType, byte[] bytes) =>
        new StringBuilder("data:").Append(mimeType).Append(";base64,").Append(Convert.ToBase64String(bytes)).ToString();

    private static byte[]? TryReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
