using Microsoft.AspNetCore.DataProtection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// hosted-project-secrets: a project's stored arbitrary-named secrets. Values are encrypted at rest
/// via ASP.NET Core Data Protection under a purpose string distinct from both
/// ConnectionStateService's and AdapterCredentialService's own protectors, so none of the three can
/// ever cross-decrypt each other's payloads. Storing (not fetching) is gated to the Paid tier — see
/// design.md's "write-time gate, not read-time" decision.
/// </summary>
public sealed class ProjectSecretService
{
    private readonly IProjectSecretRepository _repository;
    private readonly IOrganizationRepository _organizations;
    private readonly IEntitlementService _entitlements;
    private readonly IDataProtector _protector;

    public ProjectSecretService(IProjectSecretRepository repository, IOrganizationRepository organizations, IEntitlementService entitlements, IDataProtectionProvider dataProtectionProvider)
    {
        _repository = repository;
        _organizations = organizations;
        _entitlements = entitlements;
        _protector = dataProtectionProvider.CreateProtector("ReleaseTwin.ProjectSecrets.v1");
    }

    /// <exception cref="EntitlementRequiredException">The owning organization's tier lacks the <c>projectSecrets</c> entitlement.</exception>
    public async Task<ProjectSecret> SetAsync(
        Guid organizationId, Guid projectId, string name, string value, string userId, string displayName, CancellationToken cancellationToken = default)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot set a project secret: organization {organizationId} not found.");

        if (!_entitlements.For(organization).ProjectSecrets)
        {
            throw new EntitlementRequiredException("projectSecrets", "Storing project secrets requires the Team tier. Upgrade to use this feature.");
        }

        var encrypted = _protector.Protect(value);
        return await _repository.SetAsync(projectId, name, encrypted, userId, displayName, cancellationToken);
    }

    /// <summary>Returns every stored secret for a project, decrypted — an empty dictionary (never null) when nothing is stored, a distinct outcome from an auth failure.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAllDecryptedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var stored = await _repository.ListByProjectAsync(projectId, cancellationToken);
        return stored.ToDictionary(s => s.Name, s => _protector.Unprotect(s.EncryptedValue));
    }

    public Task<IReadOnlyList<ProjectSecret>> ListMetadataAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _repository.ListByProjectAsync(projectId, cancellationToken);

    public Task DeleteAsync(Guid projectId, string name, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(projectId, name, cancellationToken);
}
