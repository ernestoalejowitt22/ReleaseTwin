using System.Globalization;
using System.Net;
using System.Text;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>
/// local-evidence-viewer (design D1): turns a read evidence directory into the complete HTML report.
/// It opens no socket, writes no file, and reaches the filesystem only through
/// <see cref="EvidenceAssetLinks"/> — so every rendering requirement is testable as a string
/// assertion, and the served and exported reports are the same report by construction.
/// </summary>
public static class EvidenceReportRenderer
{
    public static string Render(EvidenceDirectoryView directory, EvidenceAssetLinks links)
    {
        var html = new StringBuilder();
        var failedCount = directory.Cases.Count(c => c.HasFailedStep);
        var title = $"ReleaseTwin evidence — {Count(directory.Cases.Count, "case")}"
            + (failedCount > 0 ? $", {failedCount} failed" : string.Empty);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{E(title)}</title>");
        html.AppendLine(links.Head());
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        html.AppendLine("<header class=\"rt-header\">");
        html.AppendLine($"<h1>{E(title)}</h1>");
        html.AppendLine($"<p class=\"rt-source\">{E(directory.SourcePath)}</p>");
        html.AppendLine("</header>");

        html.AppendLine("<div class=\"rt-layout\">");
        RenderNav(html, directory);
        html.AppendLine("<main class=\"rt-main\">");
        for (var i = 0; i < directory.Cases.Count; i++)
        {
            RenderCase(html, directory.Cases[i], i, links);
        }

        html.AppendLine("</main>");
        html.AppendLine("</div>");
        html.AppendLine(links.BodyEnd());
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    /// <summary>
    /// The case list, in the order the reader produced — failed first, so the reason a run failed is
    /// the first thing visible.
    /// </summary>
    private static void RenderNav(StringBuilder html, EvidenceDirectoryView directory)
    {
        html.AppendLine("<nav class=\"rt-nav\" aria-label=\"Cases\">");
        html.AppendLine("<h2>Cases</h2>");
        html.AppendLine("<ol>");
        for (var i = 0; i < directory.Cases.Count; i++)
        {
            var evidenceCase = directory.Cases[i];
            var failed = evidenceCase.HasFailedStep;
            html.AppendLine(
                $"<li><a href=\"#{CaseAnchor(i)}\">"
                + $"<span class=\"rt-nav-id\">{E(evidenceCase.CaseId)}</span>"
                + $"<span class=\"rt-badge rt-badge-{(failed ? "failed" : "passed")}\">{(failed ? "failed" : "passed")}</span>"
                + "</a></li>");
        }

        html.AppendLine("</ol>");
        html.AppendLine("</nav>");
    }

    private static void RenderCase(StringBuilder html, EvidenceCaseView evidenceCase, int index, EvidenceAssetLinks links)
    {
        html.AppendLine($"<section class=\"rt-case\" id=\"{CaseAnchor(index)}\">");
        html.AppendLine($"<h2>{E(evidenceCase.CaseId)}</h2>");

        if (evidenceCase.Document is not { } document)
        {
            html.AppendLine($"<p class=\"rt-error\">{E(evidenceCase.LoadError ?? "This case's evidence could not be read.")}</p>");
            html.AppendLine("</section>");
            return;
        }

        html.AppendLine($"<p class=\"rt-oracle\">{E(document.OracleLocator ?? string.Empty)}</p>");

        RenderVideo(html, evidenceCase, links);

        foreach (var leg in document.Legs)
        {
            html.AppendLine("<section class=\"rt-leg\">");
            // A named leg is a distinct labelled section; a single unnamed leg is just the steps.
            if (!string.IsNullOrEmpty(leg.Leg))
            {
                html.AppendLine($"<h3>{E(leg.Leg)}</h3>");
            }

            html.AppendLine("<ol class=\"rt-steps\">");
            foreach (var step in leg.Steps)
            {
                RenderStep(html, evidenceCase, step, links);
            }

            html.AppendLine("</ol>");
            html.AppendLine("</section>");
        }

        RenderUnreferencedScreenshots(html, evidenceCase, document, links);

        html.AppendLine($"<p class=\"rt-redaction\">{E(document.RedactionNote ?? string.Empty)}</p>");
        html.AppendLine("</section>");
    }

    private static void RenderStep(StringBuilder html, EvidenceCaseView evidenceCase, EvidenceStepDocument step, EvidenceAssetLinks links)
    {
        var outcome = step.Outcome ?? string.Empty;
        var failed = EvidenceCaseView.IsFailure(step);
        html.AppendLine($"<li class=\"rt-step{(failed ? " rt-step-failed" : string.Empty)}\">");
        html.AppendLine("<div class=\"rt-step-head\">");
        html.AppendLine($"<span class=\"rt-index\">#{step.Index.ToString(CultureInfo.InvariantCulture)}</span>");
        html.AppendLine($"<span class=\"rt-op\">{E(step.OperationName ?? string.Empty)}</span>");
        html.AppendLine($"<span class=\"rt-outcome rt-outcome-{E(outcome)}\">{E(outcome)}</span>");
        html.AppendLine($"<span class=\"rt-duration\">{step.DurationMs.ToString(CultureInfo.InvariantCulture)} ms</span>");
        html.AppendLine("</div>");

        if (step.Assertion is { } assertion)
        {
            html.AppendLine("<dl class=\"rt-assertion\">");
            html.AppendLine($"<dt>Expression</dt><dd><code>{E(assertion.Expression ?? string.Empty)}</code></dd>");
            html.AppendLine($"<dt>Expected</dt><dd><code>{E(assertion.Expected ?? "—")}</code></dd>");
            html.AppendLine($"<dt>Observed</dt><dd><code>{E(assertion.Observed ?? "—")}</code></dd>");
            html.AppendLine("</dl>");
        }

        if (step.Adapter is { } adapter)
        {
            html.AppendLine("<details class=\"rt-adapter\">");
            html.AppendLine("<summary>Adapter evidence</summary>");
            html.AppendLine($"<pre>{E(adapter.ToString(Newtonsoft.Json.Formatting.Indented))}</pre>");
            html.AppendLine("</details>");
        }

        var screenshots = step.Screenshots ?? Array.Empty<EvidenceScreenshotRef>();
        foreach (var reference in screenshots)
        {
            RenderScreenshot(html, evidenceCase, reference.Id, reference.BestEffortRedacted, links);
        }

        // The spec's "absent parts are shown as having no value rather than raising an error": a step
        // that carried none of the three optional parts says so, instead of rendering as a bare header.
        if (step.Assertion is null && step.Adapter is null && screenshots.Count == 0)
        {
            html.AppendLine("<p class=\"rt-none\">No assertion, adapter evidence, or screenshot recorded for this step.</p>");
        }

        html.AppendLine("</li>");
    }

    /// <summary>
    /// Screenshot files the document does not reference — a truncated document, or a run interrupted
    /// between writing the PNG and the JSON. They are still this case's evidence, so they are shown.
    /// </summary>
    private static void RenderUnreferencedScreenshots(
        StringBuilder html, EvidenceCaseView evidenceCase, EvidenceDocument document, EvidenceAssetLinks links)
    {
        var referenced = document.Legs
            .SelectMany(leg => leg.Steps)
            .SelectMany(step => step.Screenshots ?? Array.Empty<EvidenceScreenshotRef>())
            .Select(s => s.Id)
            .ToHashSet(StringComparer.Ordinal);

        var orphans = evidenceCase.Screenshots.Where(s => !referenced.Contains(s.Id)).ToList();
        if (orphans.Count == 0)
        {
            return;
        }

        html.AppendLine("<section class=\"rt-orphans\">");
        html.AppendLine("<h3>Screenshots not referenced by a step</h3>");
        foreach (var orphan in orphans)
        {
            RenderScreenshot(html, evidenceCase, orphan.Id, bestEffortRedacted: true, links);
        }

        html.AppendLine("</section>");
    }

    private static void RenderScreenshot(
        StringBuilder html, EvidenceCaseView evidenceCase, string id, bool bestEffortRedacted, EvidenceAssetLinks links)
    {
        var file = evidenceCase.Screenshots.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        var caption = bestEffortRedacted
            ? $"Screenshot {id} — best-effort redacted by the CLI that produced it"
            : $"Screenshot {id}";

        if (file is null)
        {
            html.AppendLine($"<p class=\"rt-none\">{E($"Screenshot {id} is referenced by this step but its file is not in the case directory.")}</p>");
            return;
        }

        var src = links.Screenshot(evidenceCase, file);
        if (src is null)
        {
            html.AppendLine($"<p class=\"rt-none\">{E($"Screenshot {id} could not be read from {file.Path}.")}</p>");
            return;
        }

        html.AppendLine("<figure class=\"rt-shot\">");
        html.AppendLine($"<img src=\"{src}\" alt=\"{E(caption)}\" loading=\"lazy\">");
        html.AppendLine($"<figcaption>{E(caption)}</figcaption>");
        html.AppendLine("</figure>");
    }

    /// <summary>
    /// Video is additive: a case with no recording renders exactly as it would if recording had never
    /// been enabled, and a recording that cannot be embedded or read says so without stopping the rest.
    /// </summary>
    private static void RenderVideo(StringBuilder html, EvidenceCaseView evidenceCase, EvidenceAssetLinks links)
    {
        var video = links.Video(evidenceCase);
        if (video.Src is null && video.Note is null)
        {
            return;
        }

        html.AppendLine("<figure class=\"rt-video\">");
        if (video.Src is not null)
        {
            html.AppendLine($"<video controls preload=\"metadata\" src=\"{video.Src}\"></video>");
            html.AppendLine("<figcaption>Session recording — local only; this is not part of the uploaded evidence document.</figcaption>");
        }
        else
        {
            html.AppendLine($"<figcaption>{E(video.Note!)}</figcaption>");
        }

        html.AppendLine("</figure>");
    }

    private static string CaseAnchor(int index) => $"case-{index.ToString(CultureInfo.InvariantCulture)}";

    private static string Count(int n, string noun) => $"{n.ToString(CultureInfo.InvariantCulture)} {noun}{(n == 1 ? string.Empty : "s")}";

    /// <summary>
    /// Everything the report shows comes from a file on disk, including case ids and adapter payloads.
    /// Every one of them goes through here.
    /// </summary>
    private static string E(string value) => WebUtility.HtmlEncode(value);
}
