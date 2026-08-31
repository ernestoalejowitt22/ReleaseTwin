using Amazon.DynamoDBv2.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>Small helpers for building/reading the <c>Dictionary&lt;string, AttributeValue&gt;</c> item shape every repository works with directly (design.md: low-level client, hand-written mapping — no DynamoDBContext).</summary>
internal static class Attrs
{
    public static AttributeValue S(string value) => new() { S = value };
    public static AttributeValue? SOrNull(string? value) => value is null ? null : new AttributeValue { S = value };
    public static AttributeValue N(long value) => new() { N = value.ToString() };
    public static AttributeValue Bool(bool value) => new() { BOOL = value };

    public static string GetS(this Dictionary<string, AttributeValue> item, string key) => item[key].S;
    public static string? GetSOrNull(this Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var v) && v.NULL != true ? v.S : null;
    public static long GetN(this Dictionary<string, AttributeValue> item, string key) => long.Parse(item[key].N);
    public static long GetNOrDefault(this Dictionary<string, AttributeValue> item, string key, long fallback = 0) =>
        item.TryGetValue(key, out var v) && v.N is not null ? long.Parse(v.N) : fallback;
    public static bool GetBool(this Dictionary<string, AttributeValue> item, string key) => item[key].BOOL == true;
    public static Guid GetGuid(this Dictionary<string, AttributeValue> item, string key) => Guid.Parse(item[key].S);
    public static DateTimeOffset GetDateTimeOffset(this Dictionary<string, AttributeValue> item, string key) =>
        DateTimeOffset.Parse(item[key].S, null, System.Globalization.DateTimeStyles.RoundtripKind);

    public static void SetIfNotNull(this Dictionary<string, AttributeValue> item, string key, AttributeValue? value)
    {
        if (value is not null)
        {
            item[key] = value;
        }
    }
}

/// <summary>Key-prefix conventions from design.md's single-table design — one place these string shapes are built, so every repository agrees on them.</summary>
internal static class Keys
{
    public static string Org(Guid orgId) => $"ORG#{orgId}";
    public static string Project(Guid projectId) => $"PROJECT#{projectId}";
    public static string Conn(Guid projectId) => $"CONN#{projectId}";
    public static string Counter(DateOnly period) => $"COUNTER#{period:yyyy-MM}";
    public static string User(string clerkUserId) => $"USER#{clerkUserId}";

    // org-membership: membership + invitation keys. Membership lives under the org partition
    // (PK=ORG#<orgId>, SK=MEMBER#<userId>) with an overloaded GSI1 entry keyed by the internal
    // AppUser id for the reverse "orgs for a user" lookup. The invitation token carries the org id
    // as its prefix so the accept flow can locate the item without a secondary index.
    public static string Member(Guid userId) => $"MEMBER#{userId}";
    public static string UserId(Guid userId) => $"USER#{userId}";
    public static string Invite(string token) => $"INVITE#{token}";
    public static string InviteClaim(string token) => $"INVITECLAIM#{token}";
    public static string Token(string tokenHash) => $"TOKEN#{tokenHash}";
    public static string TokenId(Guid tokenId) => $"TOKENID#{tokenId}";
    public static string CaseReport(DateTimeOffset uploadedAt, Guid id) => $"CASEREPORT#{uploadedAt:O}#{id}";
    public static string FlagProof(DateTimeOffset uploadedAt, Guid id) => $"FLAGPROOF#{uploadedAt:O}#{id}";

    // trend-analytics: sort-key bounds for a half-open [from, to) windowed Query. The real item keys
    // carry a "#<guid>" suffix, so an item at exactly <to> sorts past the suffix-less upper bound and
    // is excluded, while one at exactly <from> is included — the natural bucketing semantic.
    public static string CaseReportBound(DateTimeOffset at) => $"CASEREPORT#{at:O}";
    public static string FlagProofBound(DateTimeOffset at) => $"FLAGPROOF#{at:O}";
    public static string Journey(Guid journeyId) => $"JOURNEY#{journeyId}";

    /// <summary>Zero-padded so lexicographic sort-key ordering matches numeric version ordering.</summary>
    public static string JourneyVersion(int version) => $"VERSION#{version:D10}";

    public static string AdapterCredential(string adapter) => $"ADAPTERCRED#{adapter}";

    /// <summary>billing (design.md D3): webhook idempotency item — PK == SK == <c>EVENT#&lt;providerEventId&gt;</c>.</summary>
    public static string BillingEvent(string providerEventId) => $"EVENT#{providerEventId}";

    public static string ProjectSecret(string name) => $"SECRET#{name}";

    /// <summary>run-notifications: a project's outbound notification targets — <c>PK=PROJECT#&lt;projectId&gt;</c>, <c>SK=NOTIFYTARGET#&lt;targetId&gt;</c>.</summary>
    public static string NotificationTarget(Guid targetId) => $"NOTIFYTARGET#{targetId}";

    /// <summary>evidence-sharing: a run's revocable read-only share links — <c>PK=RUN#&lt;reportId&gt;</c>, <c>SK=SHARE#&lt;tokenHash&gt;</c>.</summary>
    public static string Run(Guid reportId) => $"RUN#{reportId}";
    public static string ShareLink(string tokenHash) => $"SHARE#{tokenHash}";

    public static DateOnly CurrentUtcPeriod() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
}
