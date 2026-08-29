using Newtonsoft.Json.Linq;

namespace ReleaseTwin.Cli.Evidence;

/// <summary>
/// evidence-capture / ingest-api: the redacted, uploadable evidence document the CLI produces from a
/// run's <see cref="ReleaseTwin.Core.RunEvidence"/>. Independently defined from the hosted API's own
/// contract type, the same decoupling convention the ingest report DTOs already follow.
/// </summary>
public sealed record EvidenceDocument(
    string CaseId,
    string OracleLocator,
    IReadOnlyList<EvidenceLegDocument> Legs,
    string RedactionNote);

public sealed record EvidenceLegDocument(string? Leg, IReadOnlyList<EvidenceStepDocument> Steps);

public sealed record EvidenceStepDocument(
    int Index,
    string OperationName,
    string Outcome,
    long DurationMs,
    EvidenceAssertionDocument? Assertion,
    JToken? Adapter,
    IReadOnlyList<EvidenceScreenshotRef>? Screenshots);

public sealed record EvidenceAssertionDocument(string Expression, string? Expected, string? Observed);

/// <summary>A screenshot carried out-of-band as a multipart blob, referenced from the document by id.</summary>
public sealed record EvidenceScreenshotRef(string Id, bool BestEffortRedacted);

/// <summary>A redacted screenshot's bytes, uploaded as a separate multipart part keyed by <see cref="Id"/>.</summary>
public sealed record RedactedScreenshot(string Id, byte[] PngBytes);

/// <summary>The full result of a CLI redaction pass: the document plus the out-of-band screenshot blobs.</summary>
public sealed record RedactionResult(EvidenceDocument Document, IReadOnlyList<RedactedScreenshot> Screenshots);
