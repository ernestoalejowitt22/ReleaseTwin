using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Data.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly IHostedTable _table;

    public ProjectRepository(IHostedTable table) => _table = table;

    public async Task<Project> CreateAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            OrganizationId = organizationId,
        };
        await _table.PutItemAsync(ToItem(project), cancellationToken: cancellationToken);
        return project;
    }

    public async Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var item = await _table.GetItemAsync(Keys.Org(organizationId), Keys.Project(projectId), cancellationToken);
        return item is null ? null : ToProject(item);
    }

    public async Task<IReadOnlyList<Project>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var items = await _table.QueryAsync(Keys.Org(organizationId), "PROJECT#", cancellationToken: cancellationToken);
        return items.Select(ToProject).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }

    public async Task<bool> ExistsInOrganizationAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default) =>
        await GetAsync(organizationId, projectId, cancellationToken) is not null;

    public async Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _table.ScanByEntityTypeAsync("Project", cancellationToken);
        return items.Select(ToProject).ToList();
    }

    private static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ToItem(Project project) => new()
    {
        ["PK"] = Attrs.S(Keys.Org(project.OrganizationId)),
        ["SK"] = Attrs.S(Keys.Project(project.Id)),
        ["EntityType"] = Attrs.S("Project"),
        ["Id"] = Attrs.S(project.Id.ToString()),
        ["Name"] = Attrs.S(project.Name),
        ["CreatedAt"] = Attrs.S(project.CreatedAt.ToString("O")),
        ["OrganizationId"] = Attrs.S(project.OrganizationId.ToString()),
    };

    private static Project ToProject(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item) => new()
    {
        Id = item.GetGuid("Id"),
        Name = item.GetS("Name"),
        CreatedAt = item.GetDateTimeOffset("CreatedAt"),
        OrganizationId = item.GetGuid("OrganizationId"),
    };
}
