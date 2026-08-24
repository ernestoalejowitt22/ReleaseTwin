using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ReleaseTwin.Cli.Tests;

/// <summary>Minimal fake for CliRunner tests — just enough to satisfy create/get work item, the area-path check, and reading/writing a variable group value (flag-proof toggle).</summary>
public sealed class FakeAzureDevOpsHandler : HttpMessageHandler
{
    private int _nextId = 1;
    private readonly Dictionary<int, string> _workItems = new();
    private readonly Dictionary<string, string> _variableGroupValues = new();

    public HashSet<string> ExistingAreaPaths { get; } = new() { "TeamProject\\Area" };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.Contains("/_apis/distributedtask/variablegroups/"))
        {
            if (request.Method == HttpMethod.Patch)
            {
                var body = await request.Content!.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
                foreach (var (name, node) in body!["variables"]!.AsObject())
                {
                    _variableGroupValues[name] = node!["value"]!.GetValue<string>();
                }

                return JsonResponse(HttpStatusCode.OK, "{}");
            }

            if (request.Method == HttpMethod.Get)
            {
                var variables = string.Join(",", _variableGroupValues.Select(kv => $"\"{kv.Key}\":{{\"value\":\"{kv.Value}\"}}"));
                return JsonResponse(HttpStatusCode.OK, "{\"variables\":{" + variables + "}}");
            }
        }

        if (request.Method == HttpMethod.Post && path.Contains("/_apis/wit/workitems/$"))
        {
            var id = _nextId++;
            _workItems[id] = "New";
            return (JsonResponse(HttpStatusCode.OK, "{\"id\": " + id + ", \"fields\": {\"System.State\": \"New\"}}"));
        }

        if (request.Method == HttpMethod.Get && path.Contains("/_apis/wit/workitems/") && !path.Contains("classificationnodes"))
        {
            var id = int.Parse(path.Split('/').Last());
            return _workItems.ContainsKey(id)
                ? JsonResponse(HttpStatusCode.OK, "{\"id\": " + id + "}")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Delete && path.Contains("/_apis/wit/workitems/"))
        {
            return (JsonResponse(HttpStatusCode.OK, "{}"));
        }

        if (path.Contains("classificationnodes/areas"))
        {
            var areaPath = Uri.UnescapeDataString(path.Split("areas/").Last()).Replace('/', '\\');
            return (ExistingAreaPaths.Contains(areaPath)
                ? JsonResponse(HttpStatusCode.OK, "{}")
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        throw new InvalidOperationException($"Unhandled fake request: {request.Method} {request.RequestUri}");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };
}
