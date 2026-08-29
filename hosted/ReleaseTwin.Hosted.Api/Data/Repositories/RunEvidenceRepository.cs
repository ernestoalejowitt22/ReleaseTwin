using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class RunEvidenceRepository : IRunEvidenceRepository
{
    private readonly IHostedTable _table;

    public RunEvidenceRepository(IHostedTable table) => _table = table;

    private static string Sk(Guid reportId) => $"EVIDENCE#{reportId}";

    public async Task AddAsync(UploadedRunEvidence evidence, CancellationToken cancellationToken = default) =>
        await _table.PutItemAsync(ToItem(evidence), cancellationToken: cancellationToken);

    public async Task<UploadedRunEvidence?> GetByReportAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Project(projectId), Sk(reportId), cancellationToken);
        return item is null ? null : ToEvidence(item);
    }

    public async Task<IReadOnlyList<UploadedRunEvidence>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "EVIDENCE#", cancellationToken: cancellationToken);
        return items.Select(ToEvidence).ToList();
    }

    public async Task<IReadOnlyList<UploadedRunEvidence>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _table.ScanByEntityTypeAsync("RunEvidence", cancellationToken);
        return items.Select(ToEvidence).ToList();
    }

    public async Task DeleteAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default) =>
        await _table.DeleteItemAsync(Keys.Project(projectId), Sk(reportId), cancellationToken);

    private static Dictionary<string, AttributeValue> ToItem(UploadedRunEvidence e) => new()
    {
        ["PK"] = Attrs.S(Keys.Project(e.ProjectId)),
        ["SK"] = Attrs.S(Sk(e.ReportId)),
        ["EntityType"] = Attrs.S("RunEvidence"),
        ["Id"] = Attrs.S(e.Id.ToString()),
        ["ProjectId"] = Attrs.S(e.ProjectId.ToString()),
        ["ReportId"] = Attrs.S(e.ReportId.ToString()),
        ["ReportKind"] = Attrs.S(e.ReportKind),
        ["DocumentJson"] = Attrs.S(e.DocumentJson),
        ["ScreenshotIdsCsv"] = Attrs.S(string.Join(",", e.ScreenshotIds)),
        ["UploadedAt"] = Attrs.S(e.UploadedAt.ToString("O")),
    };

    private static UploadedRunEvidence ToEvidence(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ProjectId = item.GetGuid("ProjectId"),
        ReportId = item.GetGuid("ReportId"),
        ReportKind = item.GetS("ReportKind"),
        DocumentJson = item.GetS("DocumentJson"),
        ScreenshotIds = (item.GetSOrNull("ScreenshotIdsCsv") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        UploadedAt = item.GetDateTimeOffset("UploadedAt"),
    };
}
