using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class ConnectionRepository : IConnectionRepository
{
    private readonly IHostedTable _table;

    public ConnectionRepository(IHostedTable table) => _table = table;

    public async Task<Connection?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Conn(projectId), Keys.Conn(projectId), cancellationToken);
        return item is null ? null : ToConnection(item);
    }

    public async Task UpsertAsync(Guid projectId, string provider, string externalRepo, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(projectId, cancellationToken);
        var connection = new Connection
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ProjectId = projectId,
            Provider = provider,
            ExternalRepo = externalRepo,
            ConnectedAt = DateTimeOffset.UtcNow,
        };
        await _table.PutItemAsync(ToItem(connection), cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _table.DeleteItemAsync(Keys.Conn(projectId), Keys.Conn(projectId), cancellationToken);

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(Connection connection) => new()
    {
        ["PK"] = Attrs.S(Keys.Conn(connection.ProjectId)),
        ["SK"] = Attrs.S(Keys.Conn(connection.ProjectId)),
        ["EntityType"] = Attrs.S("Connection"),
        ["Id"] = Attrs.S(connection.Id.ToString()),
        ["Provider"] = Attrs.S(connection.Provider),
        ["ExternalRepo"] = Attrs.S(connection.ExternalRepo),
        ["ConnectedAt"] = Attrs.S(connection.ConnectedAt.ToString("O")),
        ["ProjectId"] = Attrs.S(connection.ProjectId.ToString()),
    };

    private static Connection ToConnection(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        Provider = item.GetS("Provider"),
        ExternalRepo = item.GetS("ExternalRepo"),
        ConnectedAt = item.GetDateTimeOffset("ConnectedAt"),
        ProjectId = item.GetGuid("ProjectId"),
    };
}
