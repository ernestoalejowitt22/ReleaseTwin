using Amazon.DynamoDBv2.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// usage-metering design.md: a thin abstraction over the handful of DynamoDB primitives every
/// repository actually needs, so each repository's key-building/attribute-mapping logic is written
/// once and works unchanged against either the real single-table `ReleaseTwinHosted` table
/// (<see cref="DynamoDbHostedTable"/>) or the in-memory fake (<see cref="InMemoryHostedTable"/>) used
/// by fast unit tests. Both implementations must throw <see cref="ConditionalCheckFailedException"/>
/// on a failed conditional write, matching real DynamoDB's own exception type, so callers (and tests)
/// don't need to know which backend they're talking to.
/// </summary>
public interface IHostedTable
{
    /// <summary>Strongly consistent by default — real DynamoDB's GetItem is consistent unless told otherwise, and the one place that matters most (API token auth) relies on that.</summary>
    Task<Dictionary<string, AttributeValue>?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default);

    /// <summary>If <paramref name="conditionExpression"/> is set (e.g. "attribute_not_exists(PK)") and it fails, throws <see cref="ConditionalCheckFailedException"/>.</summary>
    Task PutItemAsync(Dictionary<string, AttributeValue> item, string? conditionExpression = null, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string pk, string sk, CancellationToken cancellationToken = default);

    /// <summary>Query the primary table by partition key, optionally restricted to sort keys with a given prefix.</summary>
    Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryAsync(string pk, string? skBeginsWith = null, bool scanIndexForward = true, CancellationToken cancellationToken = default);

    /// <summary>Query a GSI (eventually consistent, per real DynamoDB semantics — never used for the security-critical token lookup).</summary>
    Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryGsiAsync(string indexName, string gsiPk, CancellationToken cancellationToken = default);

    /// <summary>Atomic increment (DynamoDB's native `ADD`) — creates the item with the given starting attributes if it doesn't exist yet.</summary>
    Task UpdateItemAddAsync(string pk, string sk, IReadOnlyDictionary<string, long> increments, IReadOnlyDictionary<string, AttributeValue> itemIfNew, CancellationToken cancellationToken = default);

    /// <summary>All-or-nothing multi-item write — used for the Organization+AppUser get-or-create.</summary>
    Task TransactWritePutAsync(IReadOnlyList<(Dictionary<string, AttributeValue> Item, string? ConditionExpression)> puts, CancellationToken cancellationToken = default);
}
