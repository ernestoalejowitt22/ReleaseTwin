using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class ProjectSecretRepository : IProjectSecretRepository
{
    private readonly IHostedTable _table;

    public ProjectSecretRepository(IHostedTable table) => _table = table;

    public async Task<ProjectSecret> SetAsync(Guid projectId, string name, string encryptedValue, string lastSetByUserId, string lastSetByDisplayName, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(projectId, name, cancellationToken);
        var secret = new ProjectSecret
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            EncryptedValue = encryptedValue,
            LastSetByUserId = lastSetByUserId,
            LastSetByDisplayName = lastSetByDisplayName,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _table.PutItemAsync(ToItem(secret), cancellationToken: cancellationToken);
        return secret;
    }

    public async Task<ProjectSecret?> GetAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Project(projectId), Keys.ProjectSecret(name), cancellationToken);
        return item is null ? null : ToSecret(item);
    }

    public async Task<IReadOnlyList<ProjectSecret>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "SECRET#", cancellationToken: cancellationToken);
        return items.Select(ToSecret).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
    }

    public async Task DeleteAsync(Guid projectId, string name, CancellationToken cancellationToken = default) =>
        await _table.DeleteItemAsync(Keys.Project(projectId), Keys.ProjectSecret(name), cancellationToken);

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(ProjectSecret secret) => new()
    {
        ["PK"] = Attrs.S(Keys.Project(secret.ProjectId)),
        ["SK"] = Attrs.S(Keys.ProjectSecret(secret.Name)),
        ["EntityType"] = Attrs.S("ProjectSecret"),
        ["Id"] = Attrs.S(secret.Id.ToString()),
        ["ProjectId"] = Attrs.S(secret.ProjectId.ToString()),
        ["Name"] = Attrs.S(secret.Name),
        ["EncryptedValue"] = Attrs.S(secret.EncryptedValue),
        ["LastSetByUserId"] = Attrs.S(secret.LastSetByUserId),
        ["LastSetByDisplayName"] = Attrs.S(secret.LastSetByDisplayName),
        ["CreatedAt"] = Attrs.S(secret.CreatedAt.ToString("O")),
        ["UpdatedAt"] = Attrs.S(secret.UpdatedAt.ToString("O")),
    };

    private static ProjectSecret ToSecret(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ProjectId = item.GetGuid("ProjectId"),
        Name = item.GetS("Name"),
        EncryptedValue = item.GetS("EncryptedValue"),
        LastSetByUserId = item.GetS("LastSetByUserId"),
        LastSetByDisplayName = item.GetS("LastSetByDisplayName"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        UpdatedAt = item.GetDateTimeOffset("UpdatedAt"),
    };
}
