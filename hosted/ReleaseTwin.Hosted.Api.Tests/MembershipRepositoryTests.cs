using Amazon.DynamoDBv2.Model;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class MembershipRepositoryTests
{
    private static (MembershipRepository Memberships, InvitationRepository Invitations, InMemoryHostedTable Table) New()
    {
        var table = new InMemoryHostedTable();
        return (new MembershipRepository(table), new InvitationRepository(table), table);
    }

    private static Membership Member(Guid org, Guid user, MembershipRole role = MembershipRole.Member) =>
        new() { OrganizationId = org, UserId = user, Role = role, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task MembershipRoundTripsAndListsBothWays()
    {
        var (memberships, _, _) = New();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();

        await memberships.PutAsync(Member(orgA, user, MembershipRole.Admin));
        await memberships.PutAsync(Member(orgB, user));
        await memberships.PutAsync(Member(orgA, other));

        var byUser = await memberships.ListOrgsByUserAsync(user);
        Assert.Equal(2, byUser.Count);
        Assert.Contains(byUser, m => m.OrganizationId == orgA && m.Role == MembershipRole.Admin);
        Assert.Contains(byUser, m => m.OrganizationId == orgB && m.Role == MembershipRole.Member);

        var byOrg = await memberships.ListMembersByOrgAsync(orgA);
        Assert.Equal(2, byOrg.Count);
        Assert.Contains(byOrg, m => m.UserId == user);
        Assert.Contains(byOrg, m => m.UserId == other);

        var one = await memberships.GetAsync(orgB, user);
        Assert.Equal(MembershipRole.Member, one!.Role);

        await memberships.DeleteAsync(orgB, user);
        Assert.Null(await memberships.GetAsync(orgB, user));
        Assert.Single(await memberships.ListOrgsByUserAsync(user));
    }

    [Fact]
    public async Task ReadRepairSynthesizesFoundingAdminMembershipForLegacyUser()
    {
        var (memberships, _, _) = New();
        var org = Guid.NewGuid();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            ClerkUserId = "clerk_1",
            DisplayName = "Legacy",
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-3),
            OrganizationId = org,
        };
        var service = new MembershipService(memberships);

        var repaired = await service.GetMembershipsAsync(user);

        Assert.Single(repaired);
        Assert.Equal(org, repaired[0].OrganizationId);
        Assert.Equal(MembershipRole.Admin, repaired[0].Role);

        // Persisted: a second call reads it back, no second repair.
        var again = await memberships.GetAsync(org, user.Id);
        Assert.NotNull(again);
        Assert.Equal(MembershipRole.Admin, again!.Role);
    }

    [Fact]
    public async Task ReadRepairDoesNothingWhenUserHasNoLegacyOrg()
    {
        var (memberships, _, _) = New();
        var user = new AppUser { Id = Guid.NewGuid(), ClerkUserId = "c", DisplayName = "New", CreatedAt = DateTimeOffset.UtcNow, OrganizationId = Guid.Empty };
        var service = new MembershipService(memberships);

        Assert.Empty(await service.GetMembershipsAsync(user));
    }

    [Fact]
    public async Task InvitationTokenEncodesOrganizationId()
    {
        var org = Guid.NewGuid();
        var token = InvitationRepository.NewToken(org);
        Assert.True(InvitationRepository.TryParseOrganizationId(token, out var parsed));
        Assert.Equal(org, parsed);
        Assert.False(InvitationRepository.TryParseOrganizationId("not-a-token", out _));
    }

    [Fact]
    public async Task InvitationClaimIsSingleUse()
    {
        var (memberships, invitations, _) = New();
        var org = Guid.NewGuid();
        var invite = new Invitation
        {
            OrganizationId = org,
            Token = InvitationRepository.NewToken(org),
            Email = "teammate@example.com",
            Role = MembershipRole.Member,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.NewGuid(),
        };
        await invitations.PutAsync(invite);

        var firstUser = Guid.NewGuid();
        await invitations.ClaimAsync(invite, Member(org, firstUser));

        // Second acceptance of the same invite — by a different user — is rejected atomically.
        var secondUser = Guid.NewGuid();
        await Assert.ThrowsAsync<ConditionalCheckFailedException>(
            () => invitations.ClaimAsync(invite, Member(org, secondUser)));

        Assert.NotNull(await memberships.GetAsync(org, firstUser));
        Assert.Null(await memberships.GetAsync(org, secondUser));

        var stored = await invitations.GetByTokenAsync(invite.Token);
        Assert.Equal(InvitationState.Accepted, stored!.State);
    }

    [Fact]
    public async Task InvitationListAndRevoke()
    {
        var (_, invitations, _) = New();
        var org = Guid.NewGuid();
        var invite = new Invitation
        {
            OrganizationId = org,
            Token = InvitationRepository.NewToken(org),
            Email = "a@example.com",
            Role = MembershipRole.Admin,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.NewGuid(),
        };
        await invitations.PutAsync(invite);

        Assert.Single(await invitations.ListByOrgAsync(org));

        await invitations.DeleteAsync(org, invite.Token);
        Assert.Empty(await invitations.ListByOrgAsync(org));
        Assert.Null(await invitations.GetByTokenAsync(invite.Token));
    }

    [Fact]
    public void InvitationAcceptabilityRespectsStateAndExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var baseInvite = new Invitation
        {
            OrganizationId = Guid.NewGuid(),
            Token = "t",
            Email = "e",
            Role = MembershipRole.Member,
            ExpiresAt = now.AddDays(1),
            CreatedByUserId = Guid.NewGuid(),
        };
        Assert.True(baseInvite.IsAcceptable(now));

        baseInvite.State = InvitationState.Revoked;
        Assert.False(baseInvite.IsAcceptable(now));

        baseInvite.State = InvitationState.Pending;
        baseInvite.ExpiresAt = now.AddDays(-1);
        Assert.False(baseInvite.IsAcceptable(now));
    }
}
