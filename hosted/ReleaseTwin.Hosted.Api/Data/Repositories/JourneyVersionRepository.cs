using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class JourneyVersionRepository : IJourneyVersionRepository
{
    private readonly IHostedTable _table;

    public JourneyVersionRepository(IHostedTable table) => _table = table;

    public async Task<JourneyVersion> CreateAsync(Guid journeyId, int version, string yamlContent, string createdByUserId, string createdByDisplayName, CancellationToken cancellationToken = default)
    {
        var journeyVersion = new JourneyVersion
        {
            Id = Guid.NewGuid(),
            JourneyId = journeyId,
            Version = version,
            YamlContent = yamlContent,
            CreatedByUserId = createdByUserId,
            CreatedByDisplayName = createdByDisplayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        // Conditional write: this version number must not already exist for this journey — a
        // version is immutable once created, never silently overwritten (hosted-journeys spec).
        await _table.PutItemAsync(ToItem(journeyVersion), conditionExpression: "attribute_not_exists(PK)", cancellationToken: cancellationToken);
        return journeyVersion;
    }

    public async Task<JourneyVersion?> GetAsync(Guid journeyId, int version, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Journey(journeyId), Keys.JourneyVersion(version), cancellationToken);
        return item is null ? null : ToJourneyVersion(item);
    }

    public async Task<IReadOnlyList<JourneyVersion>> ListByJourneyAsync(Guid journeyId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Journey(journeyId), "VERSION#", cancellationToken: cancellationToken);
        return items.Select(ToJourneyVersion).OrderBy(v => v.Version).ToList();
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(JourneyVersion journeyVersion) => new()
    {
        ["PK"] = Attrs.S(Keys.Journey(journeyVersion.JourneyId)),
        ["SK"] = Attrs.S(Keys.JourneyVersion(journeyVersion.Version)),
        ["EntityType"] = Attrs.S("JourneyVersion"),
        ["Id"] = Attrs.S(journeyVersion.Id.ToString()),
        ["JourneyId"] = Attrs.S(journeyVersion.JourneyId.ToString()),
        ["Version"] = Attrs.N(journeyVersion.Version),
        ["YamlContent"] = Attrs.S(journeyVersion.YamlContent),
        ["CreatedByUserId"] = Attrs.S(journeyVersion.CreatedByUserId),
        ["CreatedByDisplayName"] = Attrs.S(journeyVersion.CreatedByDisplayName),
        ["CreatedAt"] = Attrs.S(journeyVersion.CreatedAt.ToString("O")),
    };

    private static JourneyVersion ToJourneyVersion(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        JourneyId = item.GetGuid("JourneyId"),
        Version = (int)item.GetN("Version"),
        YamlContent = item.GetS("YamlContent"),
        CreatedByUserId = item.GetS("CreatedByUserId"),
        CreatedByDisplayName = item.GetS("CreatedByDisplayName"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
    };
}
