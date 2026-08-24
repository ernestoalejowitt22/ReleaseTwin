using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// project-connections: a Connection is display metadata only (spec: "A connection is display
/// metadata only") — this service never touches a GitHub token, only the already-chosen repo
/// identifier handed to it after the picker flow completes.
/// </summary>
public sealed class ConnectionService
{
    private readonly HostedDbContext _db;

    public ConnectionService(HostedDbContext db) => _db = db;

    public async Task<bool> ProjectBelongsToOrganizationAsync(Guid projectId, Guid organizationId, CancellationToken cancellationToken = default) =>
        await _db.Projects.AnyAsync(p => p.Id == projectId && p.OrganizationId == organizationId, cancellationToken);

    public async Task ConnectAsync(Guid projectId, string provider, string externalRepo, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Connections.FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
        if (existing is not null)
        {
            existing.Provider = provider;
            existing.ExternalRepo = externalRepo;
            existing.ConnectedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.Connections.Add(new Connection
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Provider = provider,
                ExternalRepo = externalRepo,
                ConnectedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Connections.FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _db.Connections.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
