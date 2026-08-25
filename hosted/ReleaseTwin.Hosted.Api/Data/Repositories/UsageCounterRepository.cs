using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class UsageCounterRepository : IUsageCounterRepository
{
    private readonly IHostedTable _table;

    public UsageCounterRepository(IHostedTable table) => _table = table;

    public async Task IncrementAsync(Guid organizationId, DateOnly period, bool isFlagProof, CancellationToken cancellationToken = default)
    {
        var attribute = isFlagProof ? "FlagProofReportCount" : "CaseReportCount";
        var itemIfNew = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
        {
            ["EntityType"] = Attrs.S("UsageCounter"),
            ["OrganizationId"] = Attrs.S(organizationId.ToString()),
            ["PeriodStart"] = Attrs.S(period.ToString("O")),
        };

        await _table.UpdateItemAddAsync(
            Keys.Org(organizationId),
            Keys.Counter(period),
            new Dictionary<string, long> { [attribute] = 1 },
            itemIfNew,
            cancellationToken);
    }

    public async Task<UsageCounter> GetAsync(Guid organizationId, DateOnly period, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Org(organizationId), Keys.Counter(period), cancellationToken);
        if (item is null)
        {
            return new UsageCounter { OrganizationId = organizationId, PeriodStart = period, CaseReportCount = 0, FlagProofReportCount = 0 };
        }

        return new UsageCounter
        {
            OrganizationId = organizationId,
            PeriodStart = period,
            CaseReportCount = item.GetNOrDefault("CaseReportCount"),
            FlagProofReportCount = item.GetNOrDefault("FlagProofReportCount"),
        };
    }
}
