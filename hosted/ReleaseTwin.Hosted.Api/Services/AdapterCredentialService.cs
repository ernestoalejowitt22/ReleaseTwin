using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record SetAdapterCredentialResult(bool Success, bool UnknownAdapter, IReadOnlyList<string>? MissingFields)
{
    public static readonly SetAdapterCredentialResult Ok = new(true, false, null);
    public static readonly SetAdapterCredentialResult AdapterUnknown = new(false, true, null);
    public static SetAdapterCredentialResult Incomplete(IReadOnlyList<string> missing) => new(false, false, missing);
}

/// <summary>
/// hosted-adapter-credentials: a project's stored execution credentials for one adapter. Field
/// values are encrypted at rest via ASP.NET Core Data Protection under a purpose string distinct
/// from ConnectionStateService's own protector, so the two can never cross-decrypt each other's
/// payloads.
/// </summary>
public sealed class AdapterCredentialService
{
    private readonly IAdapterCredentialRepository _repository;
    private readonly IDataProtector _protector;

    public AdapterCredentialService(IAdapterCredentialRepository repository, IDataProtectionProvider dataProtectionProvider)
    {
        _repository = repository;
        _protector = dataProtectionProvider.CreateProtector("ReleaseTwin.AdapterCredentials.v1");
    }

    public async Task<SetAdapterCredentialResult> SetAsync(
        Guid projectId, string adapter, IReadOnlyDictionary<string, string> fields, string userId, string displayName, CancellationToken cancellationToken = default)
    {
        if (!AdapterCredentialFieldManifests.IsKnownAdapter(adapter))
        {
            return SetAdapterCredentialResult.AdapterUnknown;
        }

        var missing = AdapterCredentialFieldManifests.MissingFields(adapter, fields);
        if (missing is { Count: > 0 })
        {
            return SetAdapterCredentialResult.Incomplete(missing);
        }

        var encrypted = _protector.Protect(JsonSerializer.Serialize(fields));
        await _repository.SetAsync(projectId, adapter, encrypted, userId, displayName, cancellationToken);
        return SetAdapterCredentialResult.Ok;
    }

    /// <summary>Returns null if nothing is stored for this project+adapter — a distinct outcome from an auth failure, never conflated by callers.</summary>
    public async Task<IReadOnlyDictionary<string, string>?> GetDecryptedFieldsAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAsync(projectId, adapter, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var json = _protector.Unprotect(stored.EncryptedFields);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    }

    public Task<IReadOnlyList<AdapterCredential>> ListMetadataAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _repository.ListByProjectAsync(projectId, cancellationToken);

    public Task DeleteAsync(Guid projectId, string adapter, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(projectId, adapter, cancellationToken);
}
