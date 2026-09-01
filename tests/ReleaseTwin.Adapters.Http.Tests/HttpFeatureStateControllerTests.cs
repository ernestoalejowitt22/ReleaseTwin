using System.Net;
using System.Text;

namespace ReleaseTwin.Adapters.Http.Tests;

public class HttpFeatureStateControllerTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public List<(string Method, string Url, string? Body)> Calls { get; } = new();

        public RecordingHandler(HttpStatusCode status) => _status = status;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(_status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private static HttpFeatureStateController Build(HttpClient client, bool knownBadWhenDisabled) => new(
        client,
        featureKey: "checkout-v2",
        method: "PUT",
        urlTemplate: "https://flags.example/flags/{{featureKey}}",
        headerTemplates: new Dictionary<string, string> { ["Authorization"] = "Bearer t0ken" },
        bodyTemplate: "{ \"state\": \"{{state}}\", \"on\": {{enabled}} }",
        knownBadWhenDisabled: knownBadWhenDisabled);

    [Fact]
    public async Task DefaultPolaritySendsDisabledThenEnabled()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: true);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        Assert.Equal("{ \"state\": \"disabled\", \"on\": false }", handler.Calls[0].Body);
        Assert.Equal("{ \"state\": \"enabled\", \"on\": true }", handler.Calls[1].Body);
        Assert.Equal("https://flags.example/flags/checkout-v2", handler.Calls[0].Url);
        Assert.Equal("PUT", handler.Calls[0].Method);
    }

    [Fact]
    public async Task KnownBadWhenEnabledInvertsPolarity()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: false);

        await controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None);
        await controller.SetStateAsync("checkout-v2", enabled: true, CancellationToken.None);

        Assert.Equal("{ \"state\": \"enabled\", \"on\": true }", handler.Calls[0].Body);
        Assert.Equal("{ \"state\": \"disabled\", \"on\": false }", handler.Calls[1].Body);
    }

    [Fact]
    public async Task NonSuccessStatusThrowsFlagControlException()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler);
        var controller = Build(client, knownBadWhenDisabled: true);

        var ex = await Assert.ThrowsAsync<FlagControlException>(
            () => controller.SetStateAsync("checkout-v2", enabled: false, CancellationToken.None));

        Assert.Contains("500", ex.Message);
        Assert.Contains("https://flags.example/flags/checkout-v2", ex.Message);
    }
}
