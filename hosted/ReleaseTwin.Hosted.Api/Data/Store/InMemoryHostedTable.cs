using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// design.md: hand-rolled in-memory fake of the single `ReleaseTwinHosted` table, faithful to the
/// specific DynamoDB semantics this codebase actually relies on — conditional writes (throws
/// <see cref="ConditionalCheckFailedException"/>, same as real DynamoDB), atomic ADD increments, and
/// all-or-nothing transactional puts — so unit tests exercise real failure modes, not an idealized
/// happy-path-only stand-in. Only supports the specific condition-expression shape this codebase uses
/// ("attribute_not_exists(&lt;attr&gt;)"), not arbitrary DynamoDB expression syntax.
/// </summary>
public sealed class InMemoryHostedTable : IHostedTable
{
    private static readonly Regex AttributeNotExistsPattern = new(@"^attribute_not_exists\((\w+)\)$", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<(string Pk, string Sk), Dictionary<string, AttributeValue>> _items = new();
    private readonly object _writeLock = new();

    public Task<Dictionary<string, AttributeValue>?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.TryGetValue((pk, sk), out var item) ? Clone(item) : null);

    public Task PutItemAsync(Dictionary<string, AttributeValue> item, string? conditionExpression = null, CancellationToken cancellationToken = default)
    {
        var key = (item["PK"].S, item["SK"].S);
        lock (_writeLock)
        {
            CheckCondition(key, conditionExpression);
            _items[key] = Clone(item);
        }
        return Task.CompletedTask;
    }

    public Task DeleteItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
    {
        _items.TryRemove((pk, sk), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryAsync(string pk, string? skBeginsWith = null, bool scanIndexForward = true, CancellationToken cancellationToken = default)
    {
        var matches = _items
            .Where(kv => kv.Key.Pk == pk && (skBeginsWith is null || kv.Key.Sk.StartsWith(skBeginsWith, StringComparison.Ordinal)))
            .OrderBy(kv => kv.Key.Sk, StringComparer.Ordinal);
        var ordered = scanIndexForward ? matches : matches.Reverse();
        return Task.FromResult<IReadOnlyList<Dictionary<string, AttributeValue>>>(ordered.Select(kv => Clone(kv.Value)).ToList());
    }

    public Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryRangeAsync(string pk, string skFrom, string skTo, bool scanIndexForward = true, CancellationToken cancellationToken = default)
    {
        var matches = _items
            .Where(kv => kv.Key.Pk == pk
                && string.CompareOrdinal(kv.Key.Sk, skFrom) >= 0
                && string.CompareOrdinal(kv.Key.Sk, skTo) <= 0)
            .OrderBy(kv => kv.Key.Sk, StringComparer.Ordinal);
        var ordered = scanIndexForward ? matches : matches.Reverse();
        return Task.FromResult<IReadOnlyList<Dictionary<string, AttributeValue>>>(ordered.Select(kv => Clone(kv.Value)).ToList());
    }

    public Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryGsiAsync(string indexName, string gsiPk, CancellationToken cancellationToken = default)
    {
        var pkAttr = indexName == "GSI1" ? "GSI1PK" : "GSI2PK";
        var skAttr = indexName == "GSI1" ? "GSI1SK" : "GSI2SK";
        var matches = _items.Values
            .Where(v => v.TryGetValue(pkAttr, out var v1) && v1.S == gsiPk)
            .OrderByDescending(v => v.TryGetValue(skAttr, out var v2) ? v2.S : "", StringComparer.Ordinal)
            .Select(Clone)
            .ToList();
        return Task.FromResult<IReadOnlyList<Dictionary<string, AttributeValue>>>(matches);
    }

    public Task<IReadOnlyList<Dictionary<string, AttributeValue>>> ScanByEntityTypeAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var matches = _items.Values
            .Where(v => v.TryGetValue("EntityType", out var v1) && v1.S == entityType)
            .Select(Clone)
            .ToList();
        return Task.FromResult<IReadOnlyList<Dictionary<string, AttributeValue>>>(matches);
    }

    public Task UpdateItemAddAsync(string pk, string sk, IReadOnlyDictionary<string, long> increments, IReadOnlyDictionary<string, AttributeValue> itemIfNew, CancellationToken cancellationToken = default)
    {
        lock (_writeLock)
        {
            var key = (pk, sk);
            if (!_items.TryGetValue(key, out var existing))
            {
                existing = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = Attrs.S(pk),
                    ["SK"] = Attrs.S(sk),
                };
                foreach (var (attribute, value) in itemIfNew)
                {
                    existing[attribute] = value;
                }
                foreach (var (attribute, _) in increments)
                {
                    existing.TryAdd(attribute, Attrs.N(0));
                }
            }

            foreach (var (attribute, delta) in increments)
            {
                var current = existing.TryGetValue(attribute, out var v) && v.N is not null ? long.Parse(v.N) : 0;
                existing[attribute] = Attrs.N(current + delta);
            }

            _items[key] = existing;
        }
        return Task.CompletedTask;
    }

    public Task TransactWritePutAsync(IReadOnlyList<(Dictionary<string, AttributeValue> Item, string? ConditionExpression)> puts, CancellationToken cancellationToken = default)
    {
        lock (_writeLock)
        {
            // All-or-nothing: validate every condition before writing anything.
            foreach (var (item, condition) in puts)
            {
                var key = (item["PK"].S, item["SK"].S);
                try
                {
                    CheckCondition(key, condition);
                }
                catch (ConditionalCheckFailedException)
                {
                    throw;
                }
            }

            foreach (var (item, _) in puts)
            {
                var key = (item["PK"].S, item["SK"].S);
                _items[key] = Clone(item);
            }
        }
        return Task.CompletedTask;
    }

    private void CheckCondition((string Pk, string Sk) key, string? conditionExpression)
    {
        if (conditionExpression is null)
        {
            return;
        }

        var match = AttributeNotExistsPattern.Match(conditionExpression);
        if (!match.Success)
        {
            throw new NotSupportedException($"InMemoryHostedTable only supports 'attribute_not_exists(<attr>)' conditions, got: {conditionExpression}");
        }

        var attribute = match.Groups[1].Value;
        if (_items.TryGetValue(key, out var existing) && existing.ContainsKey(attribute))
        {
            throw new ConditionalCheckFailedException($"Condition '{conditionExpression}' failed for item {key}.");
        }
    }

    private static Dictionary<string, AttributeValue> Clone(Dictionary<string, AttributeValue> item) => new(item);
}
