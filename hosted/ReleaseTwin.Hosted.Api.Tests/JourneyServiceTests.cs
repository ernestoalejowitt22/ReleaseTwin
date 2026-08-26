using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>hosted-journeys: version immutability, sequential numbering, and project scoping — the logic behind /api/journeys.</summary>
public class JourneyServiceTests
{
    private static JourneyService NewService()
    {
        var table = new InMemoryHostedTable();
        return new JourneyService(new JourneyRepository(table), new JourneyVersionRepository(table));
    }

    [Fact]
    public async Task VersionsAreAssignedSequentiallyStartingAtOne()
    {
        var journeys = NewService();
        var journey = await journeys.CreateJourneyAsync(Guid.NewGuid(), "My Journey");

        var v1 = await journeys.CreateVersionAsync(journey.Id, "pipeline: []", "user-1", "Alice");
        var v2 = await journeys.CreateVersionAsync(journey.Id, "pipeline: [step]", "user-1", "Alice");

        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
    }

    // Scenario: Editing a journey does not alter a previously fetched version
    [Fact]
    public async Task EditingAJourneyLeavesEarlierVersionsUnchanged()
    {
        var journeys = NewService();
        var journey = await journeys.CreateJourneyAsync(Guid.NewGuid(), "My Journey");
        var v1 = await journeys.CreateVersionAsync(journey.Id, "original content", "user-1", "Alice");

        await journeys.CreateVersionAsync(journey.Id, "edited content", "user-1", "Alice");

        var refetched = await journeys.GetVersionAsync(journey.Id, v1.Version);
        Assert.Equal("original content", refetched!.YamlContent);
    }

    [Fact]
    public async Task VersionHistoryRecordsAuthorAndIsMostRecentFirst()
    {
        var journeys = NewService();
        var journey = await journeys.CreateJourneyAsync(Guid.NewGuid(), "My Journey");
        await journeys.CreateVersionAsync(journey.Id, "v1", "user-1", "Alice");
        await journeys.CreateVersionAsync(journey.Id, "v2", "user-2", "Bob");

        var history = await journeys.ListVersionHistoryAsync(journey.Id);

        Assert.Equal(new[] { 2, 1 }, history.Select(v => v.Version));
        Assert.Equal("Bob", history[0].CreatedByDisplayName);
        Assert.Equal("Alice", history[1].CreatedByDisplayName);
    }

    [Fact]
    public async Task AJourneyIsOnlyOwnedByTheProjectItWasCreatedUnder()
    {
        var journeys = NewService();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var journey = await journeys.CreateJourneyAsync(projectA, "My Journey");

        Assert.True(await journeys.ProjectOwnsJourneyAsync(projectA, journey.Id));
        Assert.False(await journeys.ProjectOwnsJourneyAsync(projectB, journey.Id));
    }

    [Fact]
    public async Task ListingJourneysOnlyReturnsThatProjectsJourneys()
    {
        var journeys = NewService();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await journeys.CreateJourneyAsync(projectA, "A's journey");
        await journeys.CreateJourneyAsync(projectB, "B's journey");

        var list = await journeys.ListJourneysAsync(projectA);

        Assert.Equal(new[] { "A's journey" }, list.Select(j => j.Name));
    }

    [Fact]
    public async Task FetchingAnUnknownVersionReturnsNull()
    {
        var journeys = NewService();
        var journey = await journeys.CreateJourneyAsync(Guid.NewGuid(), "My Journey");

        var result = await journeys.GetVersionAsync(journey.Id, 99);

        Assert.Null(result);
    }
}
