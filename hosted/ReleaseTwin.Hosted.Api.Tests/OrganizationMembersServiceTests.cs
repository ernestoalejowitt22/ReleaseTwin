using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ReleaseTwin.Hosted.Api.Tests;

public class OrganizationMembersServiceTests
{
    private sealed class Harness
    {
        public InMemoryHostedTable Table { get; } = new();
        public OrganizationRepository Orgs { get; }
        public MembershipRepository Memberships { get; }
        public InvitationRepository Invitations { get; }
        public ProjectRepository Projects { get; }
        public OrganizationMembersService Service { get; }
        public ProvisioningService Provisioning { get; }

        public Harness()
        {
            Orgs = new OrganizationRepository(Table);
            Memberships = new MembershipRepository(Table);
            Invitations = new InvitationRepository(Table);
            Projects = new ProjectRepository(Table);
            var membershipService = new MembershipService(Memberships);
            Service = new OrganizationMembersService(Orgs, Memberships, Invitations, Projects, membershipService,
                new LoggingInvitationEmailSender(NullLogger<LoggingInvitationEmailSender>.Instance));
            Provisioning = new ProvisioningService(
                new UserRepository(Table), Orgs, Projects, new ApiTokenRepository(Table),
                new TokenService(), TestEntitlements.Service, invitations: Invitations);
        }
    }

    [Fact]
    public async Task InviteThenAcceptCreatesMembershipWithInvitedRole()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "teammate@example.com", MembershipRole.Member);

        var teammate = await h.Provisioning.GetOrCreateUserAsync("clerk-mate", "Mate", "teammate@example.com", invite.Token);
        Assert.Equal(Guid.Empty, teammate.OrganizationId); // no throwaway org — joined via invite

        var result = await h.Service.AcceptAsync(teammate, invite.Token);
        Assert.Equal(owner.OrganizationId, result.OrganizationId);
        Assert.Equal(MembershipRole.Member, result.Role);

        var membership = await h.Memberships.GetAsync(owner.OrganizationId, teammate.Id);
        Assert.Equal(MembershipRole.Member, membership!.Role);
        Assert.Equal("teammate@example.com", membership.Email);
    }

    [Fact]
    public async Task AcceptReconcilesAwayAnEmptyAutoCreatedOrg()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "late@example.com", MembershipRole.Admin);

        // Invitee signed up WITHOUT the token forwarded → got their own auto-created org.
        var late = await h.Provisioning.GetOrCreateUserAsync("clerk-late", "Late", "late@example.com");
        var throwawayOrg = late.OrganizationId;
        Assert.NotEqual(Guid.Empty, throwawayOrg);

        await h.Service.AcceptAsync(late, invite.Token);

        Assert.NotNull(await h.Memberships.GetAsync(owner.OrganizationId, late.Id));
        Assert.Null(await h.Orgs.GetAsync(throwawayOrg));
        Assert.Null(await h.Memberships.GetAsync(throwawayOrg, late.Id));
    }

    [Fact]
    public async Task AcceptKeepsAnAutoCreatedOrgThatHasProjects()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "busy@example.com", MembershipRole.Member);

        var busy = await h.Provisioning.GetOrCreateUserAsync("clerk-busy", "Busy", "busy@example.com");
        await h.Projects.CreateAsync(busy.OrganizationId, "their-own-project");

        await h.Service.AcceptAsync(busy, invite.Token);

        Assert.NotNull(await h.Orgs.GetAsync(busy.OrganizationId)); // not reconciled — it has a project
        Assert.NotNull(await h.Memberships.GetAsync(owner.OrganizationId, busy.Id));
    }

    [Fact]
    public async Task ExpiredOrRevokedInvitationIsRejected()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invitee = await h.Provisioning.GetOrCreateUserAsync("clerk-invitee", "Invitee", "x@example.com");

        var expired = new Invitation
        {
            OrganizationId = owner.OrganizationId,
            Token = InvitationRepository.NewToken(owner.OrganizationId),
            Email = "x@example.com",
            Role = MembershipRole.Member,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
            CreatedByUserId = owner.Id,
        };
        await h.Invitations.PutAsync(expired);
        await Assert.ThrowsAsync<InvitationInvalidException>(() => h.Service.AcceptAsync(invitee, expired.Token));

        var revoked = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "x@example.com", MembershipRole.Member);
        await h.Service.RevokeInvitationAsync(owner.OrganizationId, revoked.Token);
        await Assert.ThrowsAsync<InvitationInvalidException>(() => h.Service.AcceptAsync(invitee, revoked.Token));
    }

    [Fact]
    public async Task RoleIsFixedAtIssueTime()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "m@example.com", MembershipRole.Member);

        var mate = await h.Provisioning.GetOrCreateUserAsync("clerk-m", "M", "m@example.com", invite.Token);
        var result = await h.Service.AcceptAsync(mate, invite.Token);
        Assert.Equal(MembershipRole.Member, result.Role); // not elevated during acceptance
    }

    [Fact]
    public async Task ChangeRoleAndRemoveRespectLastAdmin()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.ChangeRoleAsync(owner.OrganizationId, owner.Id, MembershipRole.Member));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.RemoveMemberAsync(owner.OrganizationId, owner.Id));

        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "admin2@example.com", MembershipRole.Admin);
        var admin2 = await h.Provisioning.GetOrCreateUserAsync("clerk-a2", "A2", "admin2@example.com", invite.Token);
        await h.Service.AcceptAsync(admin2, invite.Token);

        // Now the original owner can be demoted.
        await h.Service.ChangeRoleAsync(owner.OrganizationId, owner.Id, MembershipRole.Member);
        Assert.Equal(MembershipRole.Member, (await h.Memberships.GetAsync(owner.OrganizationId, owner.Id))!.Role);
    }

    [Fact]
    public async Task ViewerIsAnInvitableRole()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "watcher@example.com", MembershipRole.Viewer);

        var watcher = await h.Provisioning.GetOrCreateUserAsync("clerk-w", "Watcher", "watcher@example.com", invite.Token);
        var result = await h.Service.AcceptAsync(watcher, invite.Token);

        Assert.Equal(MembershipRole.Viewer, result.Role);
        Assert.Equal(MembershipRole.Viewer, (await h.Memberships.GetAsync(owner.OrganizationId, watcher.Id))!.Role);
    }

    [Fact]
    public async Task DemotingTheLastAdminToViewerIsRefused()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.ChangeRoleAsync(owner.OrganizationId, owner.Id, MembershipRole.Viewer));
    }

    [Fact]
    public async Task CreateAdditionalOrganizationMakesCreatorAdmin()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var first = user.OrganizationId;

        var second = await h.Service.CreateOrganizationAsync(user, "Side Project");

        Assert.NotEqual(first, second.Id);
        Assert.Equal(MembershipRole.Admin, (await h.Memberships.GetAsync(second.Id, user.Id))!.Role);
        Assert.NotNull(await h.Orgs.GetAsync(first)); // original org untouched
    }
}
