using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class FlagProofReportRepository : IFlagProofReportRepository
{
    private readonly IHostedTable _table;

    public FlagProofReportRepository(IHostedTable table) => _table = table;

    public async Task AddAsync(UploadedFlagProofReport report, CancellationToken cancellationToken = default) =>
        await _table.PutItemAsync(ToItem(report), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<UploadedFlagProofReport>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "FLAGPROOF#", scanIndexForward: false, cancellationToken: cancellationToken);
        return items.Select(ToReport).ToList();
    }

    public async Task<IReadOnlyList<UploadedFlagProofReport>> ListByProjectInRangeAsync(Guid projectId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryRangeAsync(
            Keys.Project(projectId), Keys.FlagProofBound(from), Keys.FlagProofBound(to), scanIndexForward: true, cancellationToken: cancellationToken);
        return items.Select(ToReport).ToList();
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(UploadedFlagProofReport report)
    {
        var item = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Project(report.ProjectId)),
            ["SK"] = Attrs.S(Keys.FlagProof(report.UploadedAt, report.Id)),
            ["EntityType"] = Attrs.S("FlagProofReport"),
            ["Id"] = Attrs.S(report.Id.ToString()),
            ["CaseId"] = Attrs.S(report.CaseId),
            ["OracleLocator"] = Attrs.S(report.OracleLocator),
            ["BuildIdentity"] = Attrs.S(report.BuildIdentity),
            ["Outcome"] = Attrs.S(report.Outcome),
            ["UploadedAt"] = Attrs.S(report.UploadedAt.ToString("O")),
            ["ProjectId"] = Attrs.S(report.ProjectId.ToString()),
        };
        item.SetIfNotNull("KnownBadLegPassed", report.KnownBadLegPassed is null ? null : Attrs.Bool(report.KnownBadLegPassed.Value));
        item.SetIfNotNull("KnownGoodLegPassed", report.KnownGoodLegPassed is null ? null : Attrs.Bool(report.KnownGoodLegPassed.Value));
        item.SetIfNotNull("Release", Attrs.SOrNull(report.Release));
        return item;
    }

    private static UploadedFlagProofReport ToReport(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        CaseId = item.GetS("CaseId"),
        OracleLocator = item.GetS("OracleLocator"),
        BuildIdentity = item.GetS("BuildIdentity"),
        Outcome = item.GetS("Outcome"),
        KnownBadLegPassed = item.TryGetValue("KnownBadLegPassed", out var bad) && bad.NULL != true ? bad.BOOL : null,
        KnownGoodLegPassed = item.TryGetValue("KnownGoodLegPassed", out var good) && good.NULL != true ? good.BOOL : null,
        Release = item.GetSOrNull("Release"),
        UploadedAt = item.GetDateTimeOffset("UploadedAt"),
        ProjectId = item.GetGuid("ProjectId"),
    };
}
