using Microsoft.AspNetCore.DataProtection;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-adapter-credentials: set/rotate/revoke, encryption round-trip, and manifest-completeness validation.</summary>
public class AdapterCredentialServiceTests
{
    private static AdapterCredentialService NewService()
    {
        var table = new InMemoryHostedTable();
        var repository = new AdapterCredentialRepository(table);
        return new AdapterCredentialService(repository, new EphemeralDataProtectionProvider());
    }

    private static Dictionary<string, string> ValidLaunchDarklyFields() => new()
    {
        ["apiToken"] = "api-abc123",
        ["projectKey"] = "my-project",
        ["environmentKey"] = "production",
    };

    [Fact]
    public async Task SettingACompleteCredentialSucceeds()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();

        var result = await service.SetAsync(projectId, "launchdarkly", ValidLaunchDarklyFields(), "user-1", "Alice");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SettingAnUnknownAdapterIsRejected()
    {
        var service = NewService();

        var result = await service.SetAsync(Guid.NewGuid(), "not-a-real-adapter", ValidLaunchDarklyFields(), "user-1", "Alice");

        Assert.False(result.Success);
        Assert.True(result.UnknownAdapter);
    }

    [Fact]
    public async Task SettingAnIncompleteCredentialListsMissingFields()
    {
        var service = NewService();
        var fields = ValidLaunchDarklyFields();
        fields.Remove("environmentKey");

        var result = await service.SetAsync(Guid.NewGuid(), "launchdarkly", fields, "user-1", "Alice");

        Assert.False(result.Success);
        Assert.False(result.UnknownAdapter);
        Assert.Equal(new[] { "environmentKey" }, result.MissingFields);
    }

    // Scenario: Raw storage never contains a plaintext secret
    [Fact]
    public async Task StoredRepresentationIsNeverThePlaintextValue()
    {
        var table = new InMemoryHostedTable();
        var repository = new AdapterCredentialRepository(table);
        var service = new AdapterCredentialService(repository, new EphemeralDataProtectionProvider());
        var projectId = Guid.NewGuid();

        await service.SetAsync(projectId, "launchdarkly", ValidLaunchDarklyFields(), "user-1", "Alice");

        var stored = await repository.GetAsync(projectId, "launchdarkly");
        Assert.NotNull(stored);
        Assert.DoesNotContain("api-abc123", stored!.EncryptedFields);
    }

    [Fact]
    public async Task FetchingAfterSettingReturnsTheOriginalPlaintextValues()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();
        var fields = ValidLaunchDarklyFields();

        await service.SetAsync(projectId, "launchdarkly", fields, "user-1", "Alice");
        var decrypted = await service.GetDecryptedFieldsAsync(projectId, "launchdarkly");

        Assert.NotNull(decrypted);
        Assert.Equal(fields["apiToken"], decrypted!["apiToken"]);
        Assert.Equal(fields["projectKey"], decrypted["projectKey"]);
        Assert.Equal(fields["environmentKey"], decrypted["environmentKey"]);
    }

    // Scenario: Rotating replaces the value entirely
    [Fact]
    public async Task RotatingReplacesThePreviousValueEntirely()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();
        await service.SetAsync(projectId, "launchdarkly", ValidLaunchDarklyFields(), "user-1", "Alice");

        var rotated = new Dictionary<string, string> { ["apiToken"] = "api-new456", ["projectKey"] = "my-project", ["environmentKey"] = "production" };
        await service.SetAsync(projectId, "launchdarkly", rotated, "user-2", "Bob");

        var decrypted = await service.GetDecryptedFieldsAsync(projectId, "launchdarkly");
        Assert.Equal("api-new456", decrypted!["apiToken"]);
    }

    // Scenario: Revoking removes the credential from future fetches
    [Fact]
    public async Task RevokingRemovesTheCredentialFromFutureFetches()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();
        await service.SetAsync(projectId, "launchdarkly", ValidLaunchDarklyFields(), "user-1", "Alice");

        await service.DeleteAsync(projectId, "launchdarkly");

        var decrypted = await service.GetDecryptedFieldsAsync(projectId, "launchdarkly");
        Assert.Null(decrypted);
    }

    [Fact]
    public async Task FetchingAnUnconfiguredAdapterReturnsNullNotAnException()
    {
        var service = NewService();

        var decrypted = await service.GetDecryptedFieldsAsync(Guid.NewGuid(), "azure-devops");

        Assert.Null(decrypted);
    }

    [Fact]
    public async Task ListingMetadataNeverIncludesFieldValues()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();
        await service.SetAsync(projectId, "launchdarkly", ValidLaunchDarklyFields(), "user-1", "Alice");

        var list = await service.ListMetadataAsync(projectId);

        var entry = Assert.Single(list);
        Assert.Equal("launchdarkly", entry.Adapter);
        Assert.Equal("Alice", entry.LastSetByDisplayName);
        Assert.DoesNotContain("api-abc123", entry.EncryptedFields);
    }
}
