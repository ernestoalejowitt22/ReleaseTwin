using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Flags;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class NotificationDispatchServiceTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Url, string Body)> Sent { get; } = [];
        public List<IPAddress?> PinnedAddresses { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Func<HttpRequestException>? Throw { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PinnedAddresses.Add(request.Options.TryGetValue(NotificationDispatchService.PinnedAddressOption, out var ip) ? ip : null);
            if (Throw is not null)
            {
                throw Throw();
            }
            Sent.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Status);
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Harness
    {
        public InMemoryHostedTable Table { get; } = new();
        public OrganizationRepository Orgs { get; }
        public ProjectRepository Projects { get; }
        public NotificationTargetRepository Targets { get; }
        public RecordingHandler Handler { get; } = new();
        public NotificationDispatchService Service { get; }

        public Harness(bool flagOn = true)
        {
            Orgs = new OrganizationRepository(Table);
            Projects = new ProjectRepository(Table);
            Targets = new NotificationTargetRepository(Table);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:run-notifications"] = flagOn ? "true" : "false",
                ["Web:BaseUrl"] = "https://app.example.com",
            }).Build();
            var flags = new FlagService(new StaticFlagProvider(FlagRegistry.Load(), config), FlagRegistry.Load(),
                new StubContextFactory(), NullLogger<FlagService>.Instance);
            // Deterministic, offline resolver: a "10.x" host maps to that private literal, everything
            // else to a fixed public address.
            Service = new NotificationDispatchService(Orgs, Projects, Targets, TestEntitlements.Service, flags,
                new SingleClientFactory(Handler), config, NullLogger<NotificationDispatchService>.Instance,
                host => host.StartsWith("10.") ? [IPAddress.Parse(host)] : [IPAddress.Parse("93.184.216.34")]);
        }

        private sealed class StubContextFactory : IFlagContextFactory
        {
            public FlagContext Current(Organization? organization = null, Guid? projectId = null) =>
                new(TargetingKey: "org", Plan: "team", Surface: "hosted", Env: "test");
        }

        public async Task<(Guid OrgId, Project Project)> SeedAsync(PlanTier tier = PlanTier.Team)
        {
            var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier };
            await Table.PutItemAsync(OrganizationRepository.ToItem(org));
            var project = await Projects.CreateAsync(org.Id, "web");
            return (org.Id, project);
        }

        public Task AddTargetAsync(Guid projectId, string url, bool enabled = true, NotificationTargetKind kind = NotificationTargetKind.Webhook) =>
            Targets.PutAsync(new NotificationTarget { Id = Guid.NewGuid(), ProjectId = projectId, Kind = kind, Url = url, Enabled = enabled, CreatedAt = DateTimeOffset.UtcNow });

        public RunNotification Notification(Guid orgId, Guid projectId) =>
            new(orgId, projectId, Guid.NewGuid(), "case", "CLM-1", "failed", "Infrastructure");
    }

    [Fact]
    public async Task DeliversToEnabledTargetAndRecordsSuccess()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a");

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Single(h.Handler.Sent);
        Assert.Contains("CLM-1", h.Handler.Sent[0].Body);
        Assert.Contains("https://app.example.com/dashboard", h.Handler.Sent[0].Body);
        var stored = (await h.Targets.ListByProjectAsync(project.Id))[0];
        Assert.Equal("success", stored.LastOutcome);
        Assert.NotNull(stored.LastAttemptAt);
    }

    [Fact]
    public async Task SkipsDisabledTargets()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a", enabled: false);

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Empty(h.Handler.Sent);
    }

    [Fact]
    public async Task DropsWhenFlagOff()
    {
        var h = new Harness(flagOn: false);
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a");

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Empty(h.Handler.Sent);
    }

    [Fact]
    public async Task DropsWhenOrgNotEntitled()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync(PlanTier.Free);
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a");

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Empty(h.Handler.Sent);
    }

    [Fact]
    public async Task RecordsFailureOnNon2xx()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a");
        h.Handler.Status = HttpStatusCode.InternalServerError;

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        var stored = (await h.Targets.ListByProjectAsync(project.Id))[0];
        Assert.StartsWith("failed: HTTP 500", stored.LastOutcome);
    }

    [Fact]
    public async Task RevalidatesTargetUrlAtSendTimeAndSkipsPrivateAddress()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        // Stored earlier (before this check tightened, or DNS since rebound) — now points at a private range.
        await h.AddTargetAsync(project.Id, "https://10.0.0.5/hook");

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Empty(h.Handler.Sent);
        var stored = (await h.Targets.ListByProjectAsync(project.Id))[0];
        Assert.Contains("non-public", stored.LastOutcome);
    }

    // security-hardening-pre-pilot D5: the outbound request pins the connection to the address the
    // SSRF check approved (Program.cs's ConnectCallback dials it; here we just assert it is carried).
    [Fact]
    public async Task DeliveryPinsTheValidatedAddressOnTheRequest()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/a");

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        var pinned = Assert.Single(h.Handler.PinnedAddresses);
        Assert.Equal(IPAddress.Parse("93.184.216.34"), pinned); // the resolver's fixed public address
    }

    [Fact]
    public async Task SlackTargetGetsATextBody()
    {
        var h = new Harness();
        var (orgId, project) = await h.SeedAsync();
        await h.AddTargetAsync(project.Id, "https://hooks.example.com/services/x", kind: NotificationTargetKind.Slack);

        await h.Service.DispatchAsync(h.Notification(orgId, project.Id));

        Assert.Single(h.Handler.Sent);
        Assert.Contains("\"text\"", h.Handler.Sent[0].Body);
        Assert.Contains("CLM-1", h.Handler.Sent[0].Body);
    }
}
