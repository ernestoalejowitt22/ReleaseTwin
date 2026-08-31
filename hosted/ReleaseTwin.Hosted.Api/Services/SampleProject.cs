using System.Text.Json;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// onboarding-activation (design D8): a virtual, read-only project shown to an organization that has
/// not yet ingested a real run. It is never persisted, never counts toward the plan project limit,
/// and rejects every mutation. The instant the org's first real run lands it disappears and never
/// returns.
/// </summary>
public static class SampleProject
{
    // Fixed, well-known ids so the dashboard, the evidence drill-down, and the tests all agree.
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-00005a4d5001");
    public static readonly Guid PassingReportId = Guid.Parse("00000000-0000-0000-0000-00005a4d5010");
    public static readonly Guid FailingReportId = Guid.Parse("00000000-0000-0000-0000-00005a4d5011");
    public static readonly Guid FlagProofReportId = Guid.Parse("00000000-0000-0000-0000-00005a4d5012");

    public const string Name = "Example · Orders API";

    public static bool IsSampleProject(Guid projectId) => projectId == Id;

    public static bool IsSampleReport(Guid reportId) =>
        reportId == PassingReportId || reportId == FailingReportId || reportId == FlagProofReportId;

    public static DashboardProjectSummary Summary { get; } = new(Id, Name, ReadOnly: true, IsExample: true);

    private static readonly DateTimeOffset Base = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<DashboardCaseReportView> CaseReports { get; } =
    [
        new("ORD-CHECKOUT-1", Passed: true, Classification: null, CleanupStatus: "AllSucceeded",
            UploadedAt: Base, ReportId: PassingReportId, EvidenceStatus: "none"),
        new("ORD-REFUND-7", Passed: false, Classification: "Assertion", CleanupStatus: "AllSucceeded",
            UploadedAt: Base.AddMinutes(3), ReportId: FailingReportId, EvidenceStatus: "available"),
    ];

    public static IReadOnlyList<DashboardFlagProofReportView> FlagProofReports { get; } =
    [
        new("ORD-REFUND-7", BuildIdentity: "orders-api@2f9c1a", Outcome: "Passed",
            KnownBadLegPassed: true, KnownGoodLegPassed: false, UploadedAt: Base.AddMinutes(5),
            ReportId: FlagProofReportId, EvidenceStatus: "none"),
    ];

    /// <summary>The canned evidence drill-down for the failing sample case, or null for the others.
    /// Same envelope shape the real evidence endpoint returns.</summary>
    public static object? EvidenceFor(Guid reportId)
    {
        if (reportId != FailingReportId)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(FailingEvidenceDocumentJson);
        return new
        {
            document = doc.RootElement.Clone(),
            screenshotIds = Array.Empty<string>(),
            uploadedAt = Base.AddMinutes(3),
        };
    }

    private const string FailingEvidenceDocumentJson = """
    {
      "caseId": "ORD-REFUND-7",
      "oracleLocator": "tickets/ORD-REFUND-7",
      "redactionNote": "This is example data — no real request bodies or credentials are involved.",
      "legs": [
        {
          "leg": null,
          "steps": [
            { "index": 1, "operationName": "http.request POST /refunds", "outcome": "Passed", "durationMs": 128, "assertion": null, "adapter": null, "screenshots": null },
            { "index": 2, "operationName": "http.assertJsonPath $.status", "outcome": "Failed", "durationMs": 4, "assertion": { "expression": "$.status", "expected": "refunded", "observed": "pending" }, "adapter": null, "screenshots": null }
          ]
        }
      ]
    }
    """;
}
