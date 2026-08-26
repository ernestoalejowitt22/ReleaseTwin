using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class JourneyRepository : IJourneyRepository
{
    private readonly IHostedTable _table;

    public JourneyRepository(IHostedTable table) => _table = table;

    public async Task<Journey> CreateAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        var journey = new Journey
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _table.PutItemAsync(ToItem(journey), cancellationToken: cancellationToken);
        return journey;
    }

    public async Task<Journey?> GetAsync(Guid projectId, Guid journeyId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Project(projectId), Keys.Journey(journeyId), cancellationToken);
        return item is null ? null : ToJourney(item);
    }

    public async Task<IReadOnlyList<Journey>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "JOURNEY#", cancellationToken: cancellationToken);
        return items.Select(ToJourney).OrderBy(j => j.Name, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(Journey journey) => new()
    {
        ["PK"] = Attrs.S(Keys.Project(journey.ProjectId)),
        ["SK"] = Attrs.S(Keys.Journey(journey.Id)),
        ["EntityType"] = Attrs.S("Journey"),
        ["Id"] = Attrs.S(journey.Id.ToString()),
        ["Name"] = Attrs.S(journey.Name),
        ["CreatedAt"] = Attrs.S(journey.CreatedAt.ToString("O")),
        ["ProjectId"] = Attrs.S(journey.ProjectId.ToString()),
    };

    private static Journey ToJourney(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        Name = item.GetS("Name"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        ProjectId = item.GetGuid("ProjectId"),
    };
}
