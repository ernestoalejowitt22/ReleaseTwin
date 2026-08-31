using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class CaseReportRepository : ICaseReportRepository
{
    private readonly IHostedTable _table;

    public CaseReportRepository(IHostedTable table) => _table = table;

    public async Task AddAsync(UploadedCaseReport report, CancellationToken cancellationToken = default) =>
        await _table.PutItemAsync(ToItem(report), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<UploadedCaseReport>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "CASEREPORT#", scanIndexForward: false, cancellationToken: cancellationToken);
        return items.Select(ToReport).ToList();
    }

    public async Task<IReadOnlyList<UploadedCaseReport>> ListByProjectInRangeAsync(Guid projectId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryRangeAsync(
            Keys.Project(projectId), Keys.CaseReportBound(from), Keys.CaseReportBound(to), scanIndexForward: true, cancellationToken: cancellationToken);
        return items.Select(ToReport).ToList();
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(UploadedCaseReport report)
    {
        var item = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Project(report.ProjectId)),
            ["SK"] = Attrs.S(Keys.CaseReport(report.UploadedAt, report.Id)),
            ["EntityType"] = Attrs.S("CaseReport"),
            ["Id"] = Attrs.S(report.Id.ToString()),
            ["CaseId"] = Attrs.S(report.CaseId),
            ["OracleLocator"] = Attrs.S(report.OracleLocator),
            ["FixtureSha256"] = Attrs.S(report.FixtureSha256),
            ["Passed"] = Attrs.Bool(report.Passed),
            ["CleanupStatus"] = Attrs.S(report.CleanupStatus),
            ["DurationMs"] = Attrs.N(report.DurationMs),
            ["UploadedAt"] = Attrs.S(report.UploadedAt.ToString("O")),
            ["ProjectId"] = Attrs.S(report.ProjectId.ToString()),
        };
        item.SetIfNotNull("Classification", Attrs.SOrNull(report.Classification));
        item.SetIfNotNull("FailureDetail", Attrs.SOrNull(report.FailureDetail));
        item.SetIfNotNull("Release", Attrs.SOrNull(report.Release));
        return item;
    }

    private static UploadedCaseReport ToReport(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        CaseId = item.GetS("CaseId"),
        OracleLocator = item.GetS("OracleLocator"),
        FixtureSha256 = item.GetS("FixtureSha256"),
        Passed = item.GetBool("Passed"),
        Classification = item.GetSOrNull("Classification"),
        FailureDetail = item.GetSOrNull("FailureDetail"),
        Release = item.GetSOrNull("Release"),
        CleanupStatus = item.GetS("CleanupStatus"),
        DurationMs = item.GetN("DurationMs"),
        UploadedAt = item.GetDateTimeOffset("UploadedAt"),
        ProjectId = item.GetGuid("ProjectId"),
    };
}
