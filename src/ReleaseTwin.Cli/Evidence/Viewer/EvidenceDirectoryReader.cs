using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>One redacted screenshot file sitting next to a case's evidence document.</summary>
public sealed record EvidenceScreenshotFile(string Id, string Path);

/// <summary>
/// local-evidence-viewer: one case directory read off disk — its document when it parsed, or
/// <see cref="LoadError"/> when it did not. A case that fails to load is still listed: the
/// directory existing is itself evidence that the case ran, so dropping it silently would hide
/// exactly what the viewer exists to show.
/// </summary>
public sealed record EvidenceCaseView(
    string DirectoryName,
    EvidenceDocument? Document,
    IReadOnlyList<EvidenceScreenshotFile> Screenshots,
    string? LoadError,
    string? VideoPath = null)
{
    /// <summary>The document's own case id, falling back to the directory name when it did not load.</summary>
    public string CaseId => Document is { CaseId.Length: > 0 } document ? document.CaseId : DirectoryName;

    /// <summary>A case that failed to load sorts with the failures — it is a thing to look at, not a pass.</summary>
    public bool HasFailedStep =>
        LoadError is not null || (Document?.Legs.Any(leg => leg.Steps.Any(IsFailure)) ?? false);

    /// <summary>
    /// <c>ExpectedFailure</c> is a pass in effect and <c>NotExecuted</c> is a consequence, not a cause,
    /// so neither makes a case sort ahead of the rest.
    /// </summary>
    public static bool IsFailure(EvidenceStepDocument step) =>
        string.Equals(step.Outcome, nameof(StepEvidenceOutcome.Failed), StringComparison.OrdinalIgnoreCase)
        || string.Equals(step.Outcome, nameof(StepEvidenceOutcome.Timeout), StringComparison.OrdinalIgnoreCase);
}

/// <summary>An evidence directory's cases, ordered failed-first.</summary>
public sealed record EvidenceDirectoryView(string SourcePath, IReadOnlyList<EvidenceCaseView> Cases);

/// <summary>
/// Either a directory to render or the reason there is nothing to render. A missing path or a
/// directory with no case subdirectories is an <see cref="Error"/>, never an empty report.
/// </summary>
public sealed record EvidenceReadResult(EvidenceDirectoryView? Directory, string? Error);

/// <summary>
/// local-evidence-viewer: reads the layout <see cref="LocalEvidenceWriter"/> writes —
/// <c>&lt;dir&gt;/&lt;case-id&gt;/evidence.json</c> plus that case's
/// <c>&lt;screenshot-id&gt;.png</c> files — without modifying it.
/// </summary>
public static class EvidenceDirectoryReader
{
    /// <summary>
    /// Deserializing mirrors <see cref="LocalEvidenceWriter"/>'s camelCase contract. The writer uses
    /// <c>NullValueHandling.Ignore</c>, so every optional field arrives here as an <em>absent key</em>
    /// rather than a null, and lands on the record's nullable parameter as null either way.
    /// </summary>
    private static readonly JsonSerializerSettings CamelCase = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    public static EvidenceReadResult Read(string path)
    {
        if (!Directory.Exists(path))
        {
            return new EvidenceReadResult(
                null,
                File.Exists(path)
                    ? $"'{path}' is a file. Point view at the evidence directory that contains one subdirectory per case id."
                    : $"No evidence directory at '{path}'. Run with RELEASETWIN_EVIDENCE=on and RELEASETWIN_EVIDENCE_DIR set, then point view at that directory.");
        }

        var caseDirs = Directory.EnumerateDirectories(path)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (caseDirs.Count == 0)
        {
            return new EvidenceReadResult(
                null,
                $"'{path}' contains no case directories. Expected one subdirectory per case id, each holding an evidence.json.");
        }

        var cases = caseDirs.Select(ReadCase).ToList();

        if (cases.All(c => c.Document is null && c.LoadError is null))
        {
            return new EvidenceReadResult(
                null,
                $"'{path}' has subdirectories but none of them holds an evidence.json. Expected <dir>/<case-id>/evidence.json.");
        }

        // Failed-first, stable within each group: OrderBy is a stable sort in LINQ to Objects, so
        // cases that neither failed nor errored keep the ordinal order established above.
        var ordered = cases
            .Where(c => c.Document is not null || c.LoadError is not null)
            .OrderBy(c => c.HasFailedStep ? 0 : 1)
            .ToList();

        return new EvidenceReadResult(new EvidenceDirectoryView(Path.GetFullPath(path), ordered), null);
    }

    private static EvidenceCaseView ReadCase(string caseDir)
    {
        var name = Path.GetFileName(caseDir);
        var documentPath = Path.Combine(caseDir, "evidence.json");

        var screenshots = Directory.EnumerateFiles(caseDir, "*.png")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(p => new EvidenceScreenshotFile(Path.GetFileNameWithoutExtension(p), p))
            .ToList();

        if (!File.Exists(documentPath))
        {
            // Not an error and not a case: a stray subdirectory in the evidence folder is simply not
            // evidence. Read() drops these, and reports when every subdirectory is one.
            return new EvidenceCaseView(name, null, screenshots, null);
        }

        try
        {
            var document = JsonConvert.DeserializeObject<EvidenceDocument>(File.ReadAllText(documentPath), CamelCase);
            return Normalize(document) is { } normalized
                ? new EvidenceCaseView(name, normalized, screenshots, null)
                : new EvidenceCaseView(name, null, screenshots, $"'{documentPath}' is not an evidence document — it has no caseId or no legs.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new EvidenceCaseView(name, null, screenshots, $"'{documentPath}' could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// Fills in the collections a hand-edited or truncated document can leave out, so every later stage
    /// can index legs and steps without null checks. Returns null when the document is missing the two
    /// fields that make it a document at all.
    /// </summary>
    private static EvidenceDocument? Normalize(EvidenceDocument? document)
    {
        if (document is null || string.IsNullOrEmpty(document.CaseId) || document.Legs is null)
        {
            return null;
        }

        var legs = document.Legs
            .Where(leg => leg is not null)
            .Select(leg => leg with { Steps = leg.Steps is null ? Array.Empty<EvidenceStepDocument>() : leg.Steps.Where(s => s is not null).ToList() })
            .ToList();

        return document with { Legs = legs };
    }
}
