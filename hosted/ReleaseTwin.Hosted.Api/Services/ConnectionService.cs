using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// project-connections: a Connection is display metadata only (spec: "A connection is display
/// metadata only") — this service never touches a GitHub token, only the already-chosen repo
/// identifier handed to it after the picker flow completes.
/// </summary>
public sealed class ConnectionService
{
    private readonly IProjectRepository _projects;
    private readonly IConnectionRepository _connections;

    public ConnectionService(IProjectRepository projects, IConnectionRepository connections)
    {
        _projects = projects;
        _connections = connections;
    }

    public async Task<bool> ProjectBelongsToOrganizationAsync(Guid projectId, Guid organizationId, CancellationToken cancellationToken = default) =>
        await _projects.ExistsInOrganizationAsync(organizationId, projectId, cancellationToken);

    public async Task ConnectAsync(Guid projectId, string provider, string externalRepo, CancellationToken cancellationToken = default) =>
        await _connections.UpsertAsync(projectId, provider, externalRepo, cancellationToken);

    public async Task DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _connections.DeleteAsync(projectId, cancellationToken);
}
