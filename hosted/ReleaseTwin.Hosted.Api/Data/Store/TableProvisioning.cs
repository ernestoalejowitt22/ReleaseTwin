using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// usage-metering tasks.md 1.4: the single `ReleaseTwinHosted` table's shape, in one place so both the
/// documented `aws dynamodb create-table` script and local-dev auto-provisioning (against DynamoDB
/// Local only — see Program.cs) describe the identical schema.
/// </summary>
public static class TableProvisioning
{
    public static async Task EnsureTableExistsAsync(IAmazonDynamoDB client, string tableName, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.DescribeTableAsync(tableName, cancellationToken);
            return;
        }
        catch (ResourceNotFoundException)
        {
            // Falls through to create it.
        }

        await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("PK", ScalarAttributeType.S),
                new AttributeDefinition("SK", ScalarAttributeType.S),
                new AttributeDefinition("GSI1PK", ScalarAttributeType.S),
                new AttributeDefinition("GSI1SK", ScalarAttributeType.S),
                new AttributeDefinition("GSI2PK", ScalarAttributeType.S),
                new AttributeDefinition("GSI2SK", ScalarAttributeType.S),
            ],
            KeySchema =
            [
                new KeySchemaElement("PK", KeyType.HASH),
                new KeySchemaElement("SK", KeyType.RANGE),
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI1",
                    KeySchema =
                    [
                        new KeySchemaElement("GSI1PK", KeyType.HASH),
                        new KeySchemaElement("GSI1SK", KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI2",
                    KeySchema =
                    [
                        new KeySchemaElement("GSI2PK", KeyType.HASH),
                        new KeySchemaElement("GSI2SK", KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ],
        }, cancellationToken);
    }
}
