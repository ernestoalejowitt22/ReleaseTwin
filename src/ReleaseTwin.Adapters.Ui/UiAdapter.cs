using Microsoft.Playwright;
using ReleaseTwin.AdapterSdk;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// Browser-driven operations (navigate, click, fill, wait, assert) as a new adapter — the UI leg of
/// a journey (e.g. UI -> API -> API -> a third party), chained to other adapters' steps via the same
/// value-capture mechanism every other adapter uses.
///
/// Unlike every other adapter in this codebase, construction is asynchronous (launching a real
/// browser process takes an awaited round trip), so this adapter is created via <see cref="CreateAsync"/>
/// rather than a public constructor.
/// </summary>
public sealed class UiAdapter : IAdapterModule, IDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly string? _recordVideoDir;

    private UiAdapter(IPlaywright playwright, IBrowser browser, string? recordVideoDir)
    {
        _playwright = playwright;
        _browser = browser;
        _recordVideoDir = recordVideoDir;
    }

    /// <summary>
    /// ui-session-video: <paramref name="recordVideoDir"/>, when set, records the run's browser
    /// session to that directory (finalized to <c>&lt;caseId&gt;.webm</c> by <c>ui.closePage</c>).
    /// Off by default — no recording, no behavior change.
    /// </summary>
    public static async Task<UiAdapter> CreateAsync(bool headless = true, string? recordVideoDir = null, CancellationToken cancellationToken = default)
    {
        var playwright = await Playwright.CreateAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(recordVideoDir))
            {
                Directory.CreateDirectory(recordVideoDir);
            }

            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
            return new UiAdapter(playwright, browser, string.IsNullOrWhiteSpace(recordVideoDir) ? null : recordVideoDir);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    public string Name => "ui";

    public static IReadOnlyDictionary<string, string> KnownOperationCapabilities { get; } = new Dictionary<string, string>
    {
        ["ui.navigate"] = "browser:chromium",
        ["ui.click"] = "browser:chromium",
        ["ui.fill"] = "browser:chromium",
        ["ui.waitFor"] = "browser:chromium",
        ["ui.assertVisible"] = "browser:chromium",
        ["ui.assertText"] = "browser:chromium",
        ["ui.setCookie"] = "browser:chromium",
        ["ui.closePage"] = "browser:chromium",
    };

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddOperation("ui.navigate", new NavigateOperation(_browser, _recordVideoDir))
            .AddOperation("ui.click", new ClickOperation(_browser, _recordVideoDir))
            .AddOperation("ui.fill", new FillOperation(_browser, _recordVideoDir))
            .AddOperation("ui.waitFor", new WaitForOperation(_browser, _recordVideoDir))
            .AddOperation("ui.assertVisible", new AssertVisibleOperation(_browser, _recordVideoDir))
            .AddOperation("ui.assertText", new AssertTextOperation(_browser, _recordVideoDir))
            .AddOperation("ui.setCookie", new SetCookieOperation(_browser, _recordVideoDir))
            .AddCleanup("ui.closePage", new ClosePageCleanup())
            .AddCapability("browser:chromium");
    }

    public void Dispose()
    {
        _browser.CloseAsync().GetAwaiter().GetResult();
        _playwright.Dispose();
    }
}
