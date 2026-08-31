using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>Real implementation of <see cref="IHostedTable"/> against the single `ReleaseTwinHosted` table (design.md). GSI1 serves ApiToken-by-project; GSI2 serves ApiToken-by-id (revoke).</summary>
public sealed class DynamoDbHostedTable : IHostedTable
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public DynamoDbHostedTable(IAmazonDynamoDB client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    public async Task<Dictionary<string, AttributeValue>?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = Key(pk, sk),
            ConsistentRead = true,
        }, cancellationToken);
        return response.IsItemSet ? response.Item : null;
    }

    public async Task PutItemAsync(Dictionary<string, AttributeValue> item, string? conditionExpression = null, CancellationToken cancellationToken = default)
    {
        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item,
            ConditionExpression = conditionExpression,
        }, cancellationToken);
    }

    public async Task DeleteItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
    {
        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = Key(pk, sk),
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryAsync(string pk, string? skBeginsWith = null, bool scanIndexForward = true, CancellationToken cancellationToken = default)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            ScanIndexForward = scanIndexForward,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = Attrs.S(pk) },
        };

        if (skBeginsWith is null)
        {
            request.KeyConditionExpression = "PK = :pk";
        }
        else
        {
            request.KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)";
            request.ExpressionAttributeValues[":skPrefix"] = Attrs.S(skBeginsWith);
        }

        var response = await _client.QueryAsync(request, cancellationToken);
        return response.Items;
    }

    public async Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryRangeAsync(string pk, string skFrom, string skTo, bool scanIndexForward = true, CancellationToken cancellationToken = default)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            ScanIndexForward = scanIndexForward,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :from AND :to",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = Attrs.S(pk),
                [":from"] = Attrs.S(skFrom),
                [":to"] = Attrs.S(skTo),
            },
        }, cancellationToken);
        return response.Items;
    }

    public async Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryGsiAsync(string indexName, string gsiPk, CancellationToken cancellationToken = default)
    {
        var pkAttr = indexName == "GSI1" ? "GSI1PK" : "GSI2PK";
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = indexName,
            KeyConditionExpression = $"{pkAttr} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = Attrs.S(gsiPk) },
            ScanIndexForward = false,
        }, cancellationToken);
        return response.Items;
    }

    public async Task<IReadOnlyList<Dictionary<string, AttributeValue>>> ScanByEntityTypeAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var response = await _client.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                FilterExpression = "EntityType = :entityType",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":entityType"] = Attrs.S(entityType) },
                ExclusiveStartKey = lastEvaluatedKey,
            }, cancellationToken);

            results.AddRange(response.Items);
            lastEvaluatedKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        } while (lastEvaluatedKey is not null);

        return results;
    }

    public async Task UpdateItemAddAsync(string pk, string sk, IReadOnlyDictionary<string, long> increments, IReadOnlyDictionary<string, AttributeValue> itemIfNew, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();
        var setClauses = new List<string>();
        var addClauses = new List<string>();
        var i = 0;

        foreach (var (attribute, delta) in increments)
        {
            var nameKey = $"#a{i}";
            var valueKey = $":v{i}";
            names[nameKey] = attribute;
            values[valueKey] = Attrs.N(delta);
            addClauses.Add($"{nameKey} {valueKey}");
            i++;
        }

        foreach (var (attribute, value) in itemIfNew)
        {
            var nameKey = $"#a{i}";
            var valueKey = $":v{i}";
            names[nameKey] = attribute;
            values[valueKey] = value;
            setClauses.Add($"{nameKey} = if_not_exists({nameKey}, {valueKey})");
            i++;
        }

        var updateExpression = "ADD " + string.Join(", ", addClauses)
            + (setClauses.Count > 0 ? " SET " + string.Join(", ", setClauses) : "");

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = Key(pk, sk),
            UpdateExpression = updateExpression,
            ExpressionAttributeNames = names,
            ExpressionAttributeValues = values,
        }, cancellationToken);
    }

    public async Task TransactWritePutAsync(IReadOnlyList<(Dictionary<string, AttributeValue> Item, string? ConditionExpression)> puts, CancellationToken cancellationToken = default)
    {
        var request = new TransactWriteItemsRequest
        {
            TransactItems = puts.Select(p => new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _tableName,
                    Item = p.Item,
                    ConditionExpression = p.ConditionExpression,
                },
            }).ToList(),
        };

        try
        {
            await _client.TransactWriteItemsAsync(request, cancellationToken);
        }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons.Any(r => r.Code == "ConditionalCheckFailed"))
        {
            throw new ConditionalCheckFailedException("A conditional check failed within the transaction.");
        }
    }

    private static Dictionary<string, AttributeValue> Key(string pk, string sk) => new()
    {
        ["PK"] = Attrs.S(pk),
        ["SK"] = Attrs.S(sk),
    };
}
