using System.Net;
using System.Text.Json;

namespace ReleaseTwin.Adapters.AzureDevOps.Tests;

/// <summary>
/// A minimal in-memory stand-in for the Azure DevOps REST API, shaped closely enough to the real
/// responses that AzureDevOpsClient's parsing logic is genuinely exercised. Used for the fast unit-test
/// path (tasks.md 6.3); a separate, credential-requiring integration path (6.1/6.2) is not implemented
/// here — see the pause note in this session.
/// </summary>
public sealed class FakeAzureDevOpsHandler : HttpMessageHandler
{
    private int _nextId = 1;
    private readonly Dictionary<int, JsonDocument> _workItems = new();
    private readonly Dictionary<string, string> _variables = new();
    private readonly object _gate = new();

    public int MaxConcurrentCreates { get; private set; }
    private int _concurrentCreates;

    public HashSet<string> ExistingAreaPaths { get; } = new() { "TeamProject\\Area" };
    public bool SimulateAreaPathCheckFailure { get; set; }
    public TimeSpan CreateDelay { get; set; } = TimeSpan.Zero;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.RequestUri.Query;

        if (request.Method == HttpMethod.Post && path.Contains("/_apis/wit/workitems/$"))
        {
            lock (_gate)
            {
                _concurrentCreates++;
                MaxConcurrentCreates = Math.Max(MaxConcurrentCreates, _concurrentCreates);
            }

            if (CreateDelay > TimeSpan.Zero)
            {
                await Task.Delay(CreateDelay, cancellationToken);
            }

            int id;
            lock (_gate)
            {
                id = _nextId++;
                _concurrentCreates--;
            }

            var doc = JsonDocument.Parse("{\"id\": " + id + ", \"fields\": {\"System.State\": \"New\"}}");
            _workItems[id] = doc;
            return JsonResponse(HttpStatusCode.OK, doc.RootElement.GetRawText());
        }

        if (request.Method == HttpMethod.Get && path.Contains("/_apis/wit/workitems/") && !path.Contains("classificationnodes"))
        {
            var id = int.Parse(path.Split('/').Last());
            return _workItems.TryGetValue(id, out var doc)
                ? JsonResponse(HttpStatusCode.OK, doc.RootElement.GetRawText())
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Patch && path.Contains("/_apis/wit/workitems/"))
        {
            return JsonResponse(HttpStatusCode.OK, "{}");
        }

        if (request.Method == HttpMethod.Delete && path.Contains("/_apis/wit/workitems/"))
        {
            var id = int.Parse(path.Split('/').Last());
            _workItems.Remove(id);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }

        if (path.Contains("classificationnodes/areas"))
        {
            if (SimulateAreaPathCheckFailure)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var areaPath = Uri.UnescapeDataString(path.Split("areas/").Last());
            return ExistingAreaPaths.Contains(areaPath.Replace('/', '\\'))
                ? JsonResponse(HttpStatusCode.OK, "{}")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (path.Contains("distributedtask/variablegroups"))
        {
            if (request.Method == HttpMethod.Get)
            {
                var variablesJson = string.Join(",", _variables.Select(kv => $"\"{kv.Key}\":{{\"value\":\"{kv.Value}\"}}"));
                return JsonResponse(HttpStatusCode.OK, "{\"id\": 1, \"variables\": {" + variablesJson + "}}");
            }

            if (request.Method == HttpMethod.Patch)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var parsed = JsonDocument.Parse(body);
                foreach (var prop in parsed.RootElement.GetProperty("variables").EnumerateObject())
                {
                    _variables[prop.Name] = prop.Value.GetProperty("value").GetString()!;
                }

                return JsonResponse(HttpStatusCode.OK, "{}");
            }
        }

        throw new InvalidOperationException($"Unhandled fake request: {request.Method} {request.RequestUri}");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };
}
