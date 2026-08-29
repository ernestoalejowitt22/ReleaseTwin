using System.Net;
using System.Text;

namespace ReleaseTwin.Adapters.Ui.Tests;

/// <summary>
/// Serves one fixed HTML page over a real local HTTP listener, so UI adapter tests drive a real
/// browser against real (if trivial) DOM content instead of an external site — hermetic and fast.
/// </summary>
internal sealed class StaticPageServer : IDisposable
{
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html>
        <head><title>UI Adapter Test Page</title></head>
        <body>
          <p id="greeting">hello</p>
          <input id="name" />
          <input id="secret" type="password" />
          <button id="submit" onclick="
            document.getElementById('result').innerText = 'Hello, ' + document.getElementById('name').value;
            document.getElementById('result').style.display = 'block';
          ">Submit</button>
          <div id="result" style="display:none"></div>
          <pre id="cookies">__COOKIES__</pre>
        </body>
        </html>
        """;

    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();

    public string Url { get; }

    public StaticPageServer()
    {
        var port = GetFreePort();
        Url = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _ = Task.Run(ServeLoop);
    }

    private async Task ServeLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_cts.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }

            var cookieHeader = context.Request.Headers["Cookie"] ?? "";
            var html = HtmlTemplate.Replace("__COOKIES__", WebUtility.HtmlEncode(cookieHeader));
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
    }
}
