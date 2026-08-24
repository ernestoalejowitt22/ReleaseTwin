using Microsoft.AspNetCore.DataProtection;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ConnectionStateServiceTests
{
    private static ConnectionStateService NewService(TimeSpan? lifetime = null) =>
        new(new EphemeralDataProtectionProvider(), lifetime);

    [Fact]
    public void MintedStateValidatesBackToTheSameProjectId()
    {
        var service = NewService();
        var projectId = Guid.NewGuid();

        var state = service.Mint(projectId);

        Assert.Equal(projectId, service.Validate(state));
    }

    [Fact]
    public void TamperedStateIsRejected()
    {
        var service = NewService();
        var state = service.Mint(Guid.NewGuid());
        var tampered = state[..^1] + (state[^1] == 'A' ? 'B' : 'A');

        Assert.Null(service.Validate(tampered));
    }

    [Fact]
    public void ExpiredStateIsRejected()
    {
        var service = NewService(TimeSpan.FromMilliseconds(1));
        var state = service.Mint(Guid.NewGuid());
        Thread.Sleep(50);

        Assert.Null(service.Validate(state));
    }

    [Fact]
    public void GarbageInputIsRejectedNotThrown()
    {
        var service = NewService();

        Assert.Null(service.Validate("not-a-real-state-value"));
    }
}
