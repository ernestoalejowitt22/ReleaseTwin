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

    private UiAdapter(IPlaywright playwright, IBrowser browser)
    {
        _playwright = playwright;
        _browser = browser;
    }

    public static async Task<UiAdapter> CreateAsync(bool headless = true, CancellationToken cancellationToken = default)
    {
        var playwright = await Playwright.CreateAsync();
        try
        {
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
            return new UiAdapter(playwright, browser);
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
        ["ui.setCookie"] = "browser:chromium",
        ["ui.closePage"] = "browser:chromium",
    };

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddOperation("ui.navigate", new NavigateOperation(_browser))
            .AddOperation("ui.click", new ClickOperation(_browser))
            .AddOperation("ui.fill", new FillOperation(_browser))
            .AddOperation("ui.waitFor", new WaitForOperation(_browser))
            .AddOperation("ui.assertVisible", new AssertVisibleOperation(_browser))
            .AddOperation("ui.setCookie", new SetCookieOperation(_browser))
            .AddCleanup("ui.closePage", new ClosePageCleanup())
            .AddCapability("browser:chromium");
    }

    public void Dispose()
    {
        _browser.CloseAsync().GetAwaiter().GetResult();
        _playwright.Dispose();
    }
}
