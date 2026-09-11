using System.Net;
using System.Text;
using ReleaseTwin.Cli;
using ReleaseTwin.Cli.Upload;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// junit-upload-verb: `releasetwin upload-junit` — what reaches the wire, what the user is told, and
/// the dispatch ordering that would otherwise send the verb into the run fallthrough.
/// </summary>
public class JUnitUploadCommandTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <testsuites tests="2" failures="1">
          <testsuite name="checkout.spec.ts">
            <testcase name="signs in" classname="checkout.spec.ts" time="1.6"/>
            <testcase name="pays" classname="checkout.spec.ts" time="1.2">
              <failure message="expected 402, got 200">stack</failure>
            </testcase>
          </testsuite>
        </testsuites>
        """;

    private static Dictionary<string, string?> Environment(string? token = "rtw_test", string? url = "https://api.test") => new()
    {
        ["RELEASETWIN_API_TOKEN"] = token,
        ["RELEASETWIN_API_URL"] = url,
    };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public int Invocations { get; private set; }

        public CapturingHandler(HttpStatusCode status = HttpStatusCode.Created, string body = """{"recorded":2,"runUrl":"/dashboard?projectId=p1"}""")
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no HTTP request expected in this test");
    }

    private static string WriteTempXml(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"junit-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, contents);
        return path;
    }

    // ---- What reaches the wire ---------------------------------------------------------------

    [Fact]
    public async Task UploadsTheFileBytesUnmodifiedAsXml()
    {
        var path = WriteTempXml(SampleXml);
        var handler = new CapturingHandler();
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);

            Assert.Equal(0, exit);
            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal("/api/ingest/junit", handler.Request.RequestUri!.AbsolutePath);
            Assert.Equal("application/xml", handler.Request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("rtw_test", handler.Request.Headers.Authorization!.Parameter);
            // Verbatim: the document that left disk is the document that hit the wire.
            Assert.Equal(SampleXml, handler.RequestBody);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReleaseLabelIsPassedAsAnEncodedQueryParameter()
    {
        var path = WriteTempXml(SampleXml);
        var handler = new CapturingHandler();
        var output = new StringWriter();

        try
        {
            await JUnitUploadCommand.RunAsync([path, "--release", "4.2 rc/1"], Environment(), output, handler);

            Assert.Contains("release=4.2%20rc%2F1", handler.Request!.RequestUri!.Query);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NoReleaseFlagSendsNoQueryString()
    {
        var path = WriteTempXml(SampleXml);
        var handler = new CapturingHandler();
        var output = new StringWriter();

        try
        {
            await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);

            Assert.Equal(string.Empty, handler.Request!.RequestUri!.Query);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The CLI must not second-guess the document. An unfamiliar dialect goes up untouched and the
    /// platform decides — that is the whole reason parsing lives on one side only.
    /// </summary>
    [Fact]
    public async Task AnUnfamiliarDialectIsUploadedUnchanged()
    {
        const string odd = """<testsuite><testcase some-vendor-attribute="x" name="t"/></testsuite>""";
        var path = WriteTempXml(odd);
        var handler = new CapturingHandler();
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);

            Assert.Equal(0, exit);
            Assert.Equal(odd, handler.RequestBody);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- What the user is told ---------------------------------------------------------------

    [Fact]
    public async Task SuccessReportsTheRecordedCountAndRunUrl()
    {
        var path = WriteTempXml(SampleXml);
        var handler = new CapturingHandler(body: """{"recorded":5,"runUrl":"/dashboard?projectId=abc"}""");
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);
            var text = output.ToString();

            Assert.Equal(0, exit);
            Assert.Contains("5 test case(s) recorded", text);
            Assert.Contains("/dashboard?projectId=abc", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The platform's rejection names which cap or malformation was hit. That text is the useful
    /// part, so it must survive rather than being replaced by a status line.
    /// </summary>
    [Fact]
    public async Task ARejectionSurfacesThePlatformsOwnMessage()
    {
        var path = WriteTempXml(SampleXml);
        const string rejection = "The uploaded document contains 1001 test cases, more than the 1000 allowed in one upload.";
        var handler = new CapturingHandler(HttpStatusCode.BadRequest, rejection);
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);

            Assert.Equal(1, exit);
            Assert.Contains(rejection, output.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AnEmptyErrorBodyStillFailsWithTheStatus()
    {
        var path = WriteTempXml(SampleXml);
        var handler = new CapturingHandler(HttpStatusCode.RequestEntityTooLarge, "");
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(), output, handler);

            Assert.Equal(1, exit);
            Assert.Contains("413", output.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Local failures, before any network call ----------------------------------------------

    [Fact]
    public async Task AMissingTokenIsAnErrorAndMakesNoRequest()
    {
        var path = WriteTempXml(SampleXml);
        var output = new StringWriter();

        try
        {
            var exit = await JUnitUploadCommand.RunAsync([path], Environment(token: null), output, new NeverCalledHandler());

            Assert.Equal(1, exit);
            Assert.Contains("RELEASETWIN_API_TOKEN", output.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AMissingFileIsAnErrorAndMakesNoRequest()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.xml");
        var output = new StringWriter();

        var exit = await JUnitUploadCommand.RunAsync([missing], Environment(), output, new NeverCalledHandler());

        Assert.Equal(1, exit);
        Assert.Contains(missing, output.ToString());
    }

    [Theory]
    [InlineData("")]           // no file argument
    [InlineData("--release")]  // flag with no value and no file
    public async Task MalformedArgumentsPrintUsageAndFail(string joined)
    {
        var args = joined.Length == 0 ? [] : joined.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var output = new StringWriter();

        var exit = await JUnitUploadCommand.RunAsync(args, Environment(), output, new NeverCalledHandler());

        Assert.Equal(1, exit);
        Assert.Contains("Usage: releasetwin upload-junit", output.ToString());
    }

    // ---- Dispatch ordering --------------------------------------------------------------------

    /// <summary>
    /// CliEntrypoint treats an unrecognized head as a directory of cases to run. If the verb were
    /// matched below that fallthrough, this would silently attempt a run instead — the same trap the
    /// `view` branch documents. Asserting on the upload path's own message pins the ordering.
    /// </summary>
    [Fact]
    public async Task TheVerbIsDispatchedRatherThanTreatedAsACaseDirectory()
    {
        var path = WriteTempXml(SampleXml);
        var output = new StringWriter();

        try
        {
            var exit = await CliEntrypoint.RunAsync(["upload-junit", path], Environment(token: null), output);

            Assert.Equal(1, exit);
            // Only the upload path produces this; a run fallthrough would complain about cases.
            Assert.Contains("upload-junit requires RELEASETWIN_API_TOKEN", output.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TheVerbAppearsInUsage()
    {
        var output = new StringWriter();

        await CliEntrypoint.RunAsync(["--help"], Environment(), output);

        var text = output.ToString();
        Assert.Contains("upload-junit", text);
        Assert.Contains("--release", text);
    }
}
