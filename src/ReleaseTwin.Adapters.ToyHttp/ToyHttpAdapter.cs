using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.ToyHttp;

/// <summary>
/// A deliberately simple, HTTP-shaped adapter (auth + two operations + one precondition + one cleanup
/// handler) used only to stress the core/adapter-sdk seam. It simulates a REST client in-memory rather
/// than making real network calls, since correctness of a real HTTP integration is not what this adapter
/// exists to prove.
/// </summary>
public sealed class ToyHttpAdapter : IAdapterModule
{
    private readonly ToyHttpClient _client;

    public ToyHttpAdapter(string apiKey)
    {
        _client = new ToyHttpClient(apiKey);
    }

    public string Name => "toy-http";

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddPrerequisite("toyhttp.recordTypeAvailable", new RecordTypeAvailableCheck(_client))
            .AddOperation("toyhttp.createRecord", new CreateRecordOperation(_client))
            .AddOperation("toyhttp.getRecord", new GetRecordOperation(_client))
            .AddCleanup("toyhttp.deleteRecord", new DeleteRecordCleanup(_client))
            .AddCapability("http:toy");
    }
}

/// <summary>An in-memory stand-in for an authenticated REST client.</summary>
public sealed class ToyHttpClient
{
    private readonly string _apiKey;
    private readonly Dictionary<string, string> _records = new();
    private readonly HashSet<string> _availableRecordTypes = new() { "claim" };

    public ToyHttpClient(string apiKey) => _apiKey = apiKey;

    public bool IsRecordTypeAvailable(string recordType) => _availableRecordTypes.Contains(recordType);

    public string CreateRecord(string recordType, string payload)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("Missing API key.");
        }

        var id = $"{recordType}-{_records.Count + 1}";
        _records[id] = payload;
        return id;
    }

    public bool TryGetRecord(string id, out string payload) => _records.TryGetValue(id, out payload!);

    public bool DeleteRecord(string id) => _records.Remove(id);
}

internal sealed class RecordTypeAvailableCheck : IPrerequisiteCheck
{
    private readonly ToyHttpClient _client;
    public RecordTypeAvailableCheck(ToyHttpClient client) => _client = client;

    public Task<PrerequisiteResult> EvaluateAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        var available = _client.IsRecordTypeAvailable("claim");
        return Task.FromResult(available
            ? PrerequisiteResult.Satisfied()
            : PrerequisiteResult.NotSatisfied("record type 'claim' not available"));
    }
}

internal sealed class CreateRecordOperation : IOperation
{
    private readonly ToyHttpClient _client;
    public CreateRecordOperation(ToyHttpClient client) => _client = client;

    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var id = _client.CreateRecord("claim", "{}");
        context.AdapterState["toyhttp.recordId"] = id;
        return Task.FromResult(OperationResult.Pass(id));
    }
}

internal sealed class GetRecordOperation : IOperation
{
    private readonly ToyHttpClient _client;
    public GetRecordOperation(ToyHttpClient client) => _client = client;

    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("toyhttp.recordId", out var idObj) && idObj is string id
            && _client.TryGetRecord(id, out var payload))
        {
            return Task.FromResult(OperationResult.Pass(payload));
        }

        return Task.FromResult(OperationResult.Fail("record not found"));
    }
}

internal sealed class DeleteRecordCleanup : ICleanupOperation
{
    private readonly ToyHttpClient _client;
    public DeleteRecordCleanup(ToyHttpClient client) => _client = client;

    public Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("toyhttp.recordId", out var idObj) && idObj is string id)
        {
            return Task.FromResult(new CleanupResult(_client.DeleteRecord(id)));
        }

        return Task.FromResult(new CleanupResult(true));
    }
}
