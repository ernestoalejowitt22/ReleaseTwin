namespace ReleaseTwin.Core;

public enum CleanupStatus
{
    NotRun,
    AllSucceeded,
    SomeFailed,
}

public sealed record CaseReport(
    string CaseId,
    OracleReference Oracle,
    string FixtureSha256,
    bool Passed,
    FailureClassification? Classification,
    string? FailureDetail,
    CleanupStatus CleanupStatus,
    TimeSpan Duration);
