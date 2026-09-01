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
        public RecordingInvitationEmailSender Email { get; }

        public Harness() : this(new RecordingInvitationEmailSender()) { }

        public Harness(RecordingInvitationEmailSender email)
        {
            Email = email;
            Orgs = new OrganizationRepository(Table);
            Memberships = new MembershipRepository(Table);
            Invitations = new InvitationRepository(Table);
            Projects = new ProjectRepository(Table);
            var membershipService = new MembershipService(Memberships);
            Service = new OrganizationMembersService(Orgs, Memberships, Invitations, Projects, membershipService,
                email, NullLogger<OrganizationMembersService>.Instance);
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

    // security-hardening-pre-pilot D2 --------------------------------------------------------------

    [Fact]
    public async Task AcceptWithMatchingVerifiedEmailSucceeds_CaseInsensitive()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "Teammate@Example.com", MembershipRole.Member);

        var teammate = await h.Provisioning.GetOrCreateUserAsync("clerk-mate", "Mate", "teammate@example.com", invite.Token);
        var result = await h.Service.AcceptAsync(teammate, invite.Token);

        Assert.Equal(owner.OrganizationId, result.OrganizationId);
        Assert.NotNull(await h.Memberships.GetAsync(owner.OrganizationId, teammate.Id));
    }

    [Fact]
    public async Task AcceptWithNonMatchingEmailIsRefusedLikeAnInvalidInvite()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "invited@example.com", MembershipRole.Admin);

        // A different signed-in user got hold of the link.
        var attacker = await h.Provisioning.GetOrCreateUserAsync("clerk-x", "X", "someone-else@example.com");

        var ex = await Assert.ThrowsAsync<InvitationInvalidException>(() => h.Service.AcceptAsync(attacker, invite.Token));
        Assert.Equal("This invitation is no longer valid.", ex.Message); // identical to expired/revoked
        Assert.Null(await h.Memberships.GetAsync(owner.OrganizationId, attacker.Id));

        // Invite is untouched — the real invitee can still accept.
        var invitee = await h.Provisioning.GetOrCreateUserAsync("clerk-i", "I", "invited@example.com", invite.Token);
        var result = await h.Service.AcceptAsync(invitee, invite.Token);
        Assert.Equal(MembershipRole.Admin, result.Role);
    }

    [Fact]
    public async Task AcceptWithNoVerifiedEmailIsANonMatchNotABypass()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "invited@example.com", MembershipRole.Member);

        // Session carried no email claim → user provisioned with a null email + a throwaway org.
        var noEmail = await h.Provisioning.GetOrCreateUserAsync("clerk-noemail", "NoEmail", null);

        await Assert.ThrowsAsync<InvitationInvalidException>(() => h.Service.AcceptAsync(noEmail, invite.Token));
        Assert.Null(await h.Memberships.GetAsync(owner.OrganizationId, noEmail.Id));
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

    // company-and-domain-launch: invitation email is best-effort — sent when a provider is
    // configured, skipped when none is, and never fatal to the invitation.

    [Fact]
    public async Task InvitationEmailIsSentWithTheAcceptLink()
    {
        var h = new Harness();
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "teammate@example.com", MembershipRole.Member);

        await h.Service.SendInvitationEmailAsync(invite, "Acme", $"https://releasetwin.com/invitations/{invite.Token}");

        var sent = Assert.Single(h.Email.Sent);
        Assert.Equal("teammate@example.com", sent.ToEmail);
        Assert.Contains(invite.Token, sent.AcceptUrl);
    }

    [Fact]
    public async Task InvitationSucceedsAndSkipsSendWhenNoProviderIsConfigured()
    {
        // The logging fallback is what runs with no Notifications:FromAddress — it must not throw
        // and the invitation must still be created with a usable token.
        var membershipService = new MembershipService(new MembershipRepository(new InMemoryHostedTable()));
        var table = new InMemoryHostedTable();
        var invitations = new InvitationRepository(table);
        var service = new OrganizationMembersService(
            new OrganizationRepository(table), new MembershipRepository(table), invitations,
            new ProjectRepository(table), membershipService,
            new LoggingInvitationEmailSender(NullLogger<LoggingInvitationEmailSender>.Instance),
            NullLogger<OrganizationMembersService>.Instance);

        var invite = await service.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), "x@example.com", MembershipRole.Member);
        await service.SendInvitationEmailAsync(invite, "Acme", "/invitations/" + invite.Token);

        Assert.Equal(InvitationState.Pending, (await invitations.GetByTokenAsync(invite.Token))!.State);
    }

    [Fact]
    public async Task EmailProviderFailureDoesNotInvalidateTheInvitation()
    {
        var h = new Harness(new RecordingInvitationEmailSender { Throw = true });
        var owner = await h.Provisioning.GetOrCreateUserAsync("clerk-owner", "Owner", "owner@example.com");
        var invite = await h.Service.InviteAsync(owner.OrganizationId, owner.Id, "teammate@example.com", MembershipRole.Member);

        // Must not propagate.
        await h.Service.SendInvitationEmailAsync(invite, "Acme", $"https://releasetwin.com/invitations/{invite.Token}");

        var stored = await h.Invitations.GetByTokenAsync(invite.Token);
        Assert.NotNull(stored);
        Assert.Equal(InvitationState.Pending, stored!.State);

        // The invitation is still acceptable end to end.
        var teammate = await h.Provisioning.GetOrCreateUserAsync("clerk-mate", "Mate", "teammate@example.com", invite.Token);
        var result = await h.Service.AcceptAsync(teammate, invite.Token);
        Assert.Equal(owner.OrganizationId, result.OrganizationId);
    }

    internal sealed class RecordingInvitationEmailSender : IInvitationEmailSender
    {
        public bool Throw { get; init; }
        public List<(string ToEmail, string OrganizationName, string AcceptUrl)> Sent { get; } = [];

        public Task SendAsync(string toEmail, string organizationName, string acceptUrl, CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("provider unavailable");
            }

            Sent.Add((toEmail, organizationName, acceptUrl));
            return Task.CompletedTask;
        }
    }
}
