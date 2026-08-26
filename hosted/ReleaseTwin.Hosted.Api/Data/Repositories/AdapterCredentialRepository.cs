using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class AdapterCredentialRepository : IAdapterCredentialRepository
{
    private readonly IHostedTable _table;

    public AdapterCredentialRepository(IHostedTable table) => _table = table;

    public async Task<AdapterCredential> SetAsync(Guid projectId, string adapter, string encryptedFields, string lastSetByUserId, string lastSetByDisplayName, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(projectId, adapter, cancellationToken);
        var credential = new AdapterCredential
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ProjectId = projectId,
            Adapter = adapter,
            EncryptedFields = encryptedFields,
            LastSetByUserId = lastSetByUserId,
            LastSetByDisplayName = lastSetByDisplayName,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _table.PutItemAsync(ToItem(credential), cancellationToken: cancellationToken);
        return credential;
    }

    public async Task<AdapterCredential?> GetAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Project(projectId), Keys.AdapterCredential(adapter), cancellationToken);
        return item is null ? null : ToCredential(item);
    }

    public async Task<IReadOnlyList<AdapterCredential>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Project(projectId), "ADAPTERCRED#", cancellationToken: cancellationToken);
        return items.Select(ToCredential).OrderBy(c => c.Adapter, StringComparer.Ordinal).ToList();
    }

    public async Task DeleteAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default) =>
        await _table.DeleteItemAsync(Keys.Project(projectId), Keys.AdapterCredential(adapter), cancellationToken);

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(AdapterCredential credential) => new()
    {
        ["PK"] = Attrs.S(Keys.Project(credential.ProjectId)),
        ["SK"] = Attrs.S(Keys.AdapterCredential(credential.Adapter)),
        ["EntityType"] = Attrs.S("AdapterCredential"),
        ["Id"] = Attrs.S(credential.Id.ToString()),
        ["ProjectId"] = Attrs.S(credential.ProjectId.ToString()),
        ["Adapter"] = Attrs.S(credential.Adapter),
        ["EncryptedFields"] = Attrs.S(credential.EncryptedFields),
        ["LastSetByUserId"] = Attrs.S(credential.LastSetByUserId),
        ["LastSetByDisplayName"] = Attrs.S(credential.LastSetByDisplayName),
        ["CreatedAt"] = Attrs.S(credential.CreatedAt.ToString("O")),
        ["UpdatedAt"] = Attrs.S(credential.UpdatedAt.ToString("O")),
    };

    private static AdapterCredential ToCredential(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        ProjectId = item.GetGuid("ProjectId"),
        Adapter = item.GetS("Adapter"),
        EncryptedFields = item.GetS("EncryptedFields"),
        LastSetByUserId = item.GetS("LastSetByUserId"),
        LastSetByDisplayName = item.GetS("LastSetByDisplayName"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        UpdatedAt = item.GetDateTimeOffset("UpdatedAt"),
    };
}
