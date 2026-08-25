using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class ApiTokenRepository : IApiTokenRepository
{
    private readonly IHostedTable _table;

    public ApiTokenRepository(IHostedTable table) => _table = table;

    public async Task<ApiToken> CreateAsync(Guid projectId, Guid organizationId, string tokenHash, string displayPrefix, CancellationToken cancellationToken = default)
    {
        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            DisplayPrefix = displayPrefix,
            CreatedAt = DateTimeOffset.UtcNow,
            ProjectId = projectId,
            OrganizationId = organizationId,
        };
        // TokenHash is our own SHA-256 of a 256-bit random secret — collision risk is not a real
        // concern, so an unconditional PutItem is fine (design.md: TokenHash as the strongly-consistent
        // primary key, not enforced-unique via a condition since it's already effectively unique).
        await _table.PutItemAsync(ToItem(token), cancellationToken: cancellationToken);
        return token;
    }

    public async Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Token(tokenHash), Keys.Token(tokenHash), cancellationToken);
        return item is null ? null : ToToken(item);
    }

    public async Task<IReadOnlyList<ApiToken>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryGsiAsync("GSI1", Keys.Project(projectId), cancellationToken);
        return items.Select(ToToken).ToList();
    }

    public async Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var indexItems = await _table.QueryGsiAsync("GSI2", Keys.TokenId(tokenId), cancellationToken);
        var indexItem = indexItems.FirstOrDefault();
        if (indexItem is null)
        {
            return;
        }

        var tokenHash = indexItem.GetS("TokenHash");
        var full = await _table.GetItemAsync(Keys.Token(tokenHash), Keys.Token(tokenHash), cancellationToken);
        if (full is null)
        {
            return;
        }

        var token = ToToken(full);
        if (token.IsRevoked)
        {
            return;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        await _table.PutItemAsync(ToItem(token), cancellationToken: cancellationToken);
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(ApiToken token)
    {
        var item = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
        {
            ["PK"] = Attrs.S(Keys.Token(token.TokenHash)),
            ["SK"] = Attrs.S(Keys.Token(token.TokenHash)),
            ["EntityType"] = Attrs.S("ApiToken"),
            ["Id"] = Attrs.S(token.Id.ToString()),
            ["TokenHash"] = Attrs.S(token.TokenHash),
            ["DisplayPrefix"] = Attrs.S(token.DisplayPrefix),
            ["CreatedAt"] = Attrs.S(token.CreatedAt.ToString("O")),
            ["ProjectId"] = Attrs.S(token.ProjectId.ToString()),
            ["OrganizationId"] = Attrs.S(token.OrganizationId.ToString()),
            ["GSI1PK"] = Attrs.S(Keys.Project(token.ProjectId)),
            ["GSI1SK"] = Attrs.S($"TOKEN#{token.CreatedAt:O}#{token.Id}"),
            ["GSI2PK"] = Attrs.S(Keys.TokenId(token.Id)),
            ["GSI2SK"] = Attrs.S(Keys.TokenId(token.Id)),
        };
        item.SetIfNotNull("RevokedAt", token.RevokedAt is null ? null : Attrs.S(token.RevokedAt.Value.ToString("O")));
        return item;
    }

    private static ApiToken ToToken(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        TokenHash = item.GetS("TokenHash"),
        DisplayPrefix = item.GetS("DisplayPrefix"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        ProjectId = item.GetGuid("ProjectId"),
        OrganizationId = item.GetGuid("OrganizationId"),
        RevokedAt = item.TryGetValue("RevokedAt", out var v) && v.NULL != true ? DateTimeOffset.Parse(v.S) : null,
    };
}
