namespace ReleaseTwin.Hosted.Api.Data.Entities;

/// <summary>
/// org-membership: the role a <see cref="Membership"/> carries. Two roles only (spec) — <see cref="Admin"/>
/// manages billing, plan tier, tokens, members, invitations, and notification targets; <see cref="Member"/>
/// uses projects and views run history and evidence.
/// </summary>
public enum MembershipRole
{
    Member,
    Admin,
}

/// <summary>
/// org-membership: the link between an <see cref="AppUser"/> and an <see cref="Organization"/>. A user may
/// hold memberships in several organizations at once; each membership carries exactly one role. Replaces the
/// former 1:1 <see cref="AppUser.OrganizationId"/> reference (kept write-through during the compat window).
///
/// Item shape: <c>PK=ORG#&lt;orgId&gt;</c>, <c>SK=MEMBER#&lt;userId&gt;</c>, with the overloaded
/// <c>GSI1PK=USER#&lt;userId&gt;</c> / <c>GSI1SK=ORG#&lt;orgId&gt;</c> for the reverse "orgs for a user" query.
/// </summary>
public sealed class Membership
{
    public required Guid OrganizationId { get; set; }

    /// <summary>The internal <see cref="AppUser.Id"/>, not the Clerk user id.</summary>
    public required Guid UserId { get; set; }

    public required MembershipRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>org-membership: snapshot of the user's display name at join time, so the members list is
    /// self-contained (there is no user-by-id index). Refreshed opportunistically on role changes.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Snapshot of the user's email at join time. Same rationale as <see cref="DisplayName"/>.</summary>
    public string? Email { get; set; }
}

/// <summary>org-membership: lifecycle state of an <see cref="Invitation"/>. An invite is single-use — atomic
/// consumption is enforced by a separate claim item, and this field is the human-facing status for listings.</summary>
public enum InvitationState
{
    Pending,
    Accepted,
    Revoked,
}

/// <summary>
/// org-membership: an outstanding invitation for an email address to join an organization with a fixed role.
/// The <see cref="Token"/> encodes the organization id as its prefix (<c>&lt;orgId&gt;.&lt;random&gt;</c>) so
/// the accept flow — which only has the token — can locate the item without a secondary index.
///
/// Item shape: <c>PK=ORG#&lt;orgId&gt;</c>, <c>SK=INVITE#&lt;token&gt;</c>.
/// </summary>
public sealed class Invitation
{
    public required Guid OrganizationId { get; set; }

    public required string Token { get; set; }

    public required string Email { get; set; }

    public required MembershipRole Role { get; set; }

    public InvitationState State { get; set; } = InvitationState.Pending;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public required Guid CreatedByUserId { get; set; }

    /// <summary>True when the invite is neither used, revoked, nor past <see cref="ExpiresAt"/>.</summary>
    public bool IsAcceptable(DateTimeOffset now) => State == InvitationState.Pending && now < ExpiresAt;
}
