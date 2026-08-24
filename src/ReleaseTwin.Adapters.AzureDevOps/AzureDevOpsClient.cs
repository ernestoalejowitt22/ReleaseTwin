using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReleaseTwin.Adapters.AzureDevOps;

/// <summary>
/// Thin wrapper over the Azure DevOps REST API (Work Items + Distributed Task variable groups).
/// Auth and base address are set once at construction; every call is a plain HTTP request so a
/// test can substitute a fake <see cref="HttpMessageHandler"/> without touching this class.
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
{
    private const string ApiVersion = "7.1";
    private readonly HttpClient _http;

    public AzureDevOpsClient(AzureDevOpsOptions options, HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri($"https://dev.azure.com/{options.Organization}/"),
        };

        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        Project = options.Project;
    }

    public string Project { get; }

    public async Task<int> CreateWorkItemAsync(string workItemType, IReadOnlyList<JsonPatchOperation> patch, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Uri.EscapeDataString(Project)}/_apis/wit/workitems/${Uri.EscapeDataString(workItemType)}?api-version={ApiVersion}")
        {
            Content = JsonContent.Create(patch.Select(p => new { op = p.Op, path = p.Path, value = p.Value })),
        };
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return body!["id"]!.GetValue<int>();
    }

    public async Task<JsonNode?> TryGetWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"_apis/wit/workitems/{id}?api-version={ApiVersion}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
    }

    public async Task UpdateWorkItemStateAsync(int id, string state, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"_apis/wit/workitems/{id}?api-version={ApiVersion}")
        {
            Content = JsonContent.Create(new[] { new { op = "add", path = "/fields/System.State", value = state } }),
        };
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await _http.DeleteAsync($"_apis/wit/workitems/{id}?api-version={ApiVersion}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Returns true/false for a confirmed answer, or throws for a call that could not be completed
    /// (auth failure, network error, unreachable org) — the caller decides how to classify that.
    /// </summary>
    public async Task<bool> AreaPathExistsAsync(string areaPath, CancellationToken cancellationToken)
    {
        var encoded = string.Join("/", areaPath.Split('\\').Select(Uri.EscapeDataString));
        using var response = await _http.GetAsync(
            $"{Uri.EscapeDataString(Project)}/_apis/wit/classificationnodes/areas/{encoded}?api-version={ApiVersion}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<string?> GetVariableGroupValueAsync(int groupId, string variableName, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            $"{Uri.EscapeDataString(Project)}/_apis/distributedtask/variablegroups/{groupId}?api-version={ApiVersion}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return body?["variables"]?[variableName]?["value"]?.GetValue<string>();
    }

    public async Task SetVariableGroupValueAsync(int groupId, string variableName, string value, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{Uri.EscapeDataString(Project)}/_apis/distributedtask/variablegroups/{groupId}?api-version={ApiVersion}")
        {
            Content = JsonContent.Create(new
            {
                variables = new Dictionary<string, object> { [variableName] = new { value } },
            }),
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}

public sealed record JsonPatchOperation(string Op, string Path, object Value)
{
    public static JsonPatchOperation Add(string path, object value) => new("add", path, value);
}
