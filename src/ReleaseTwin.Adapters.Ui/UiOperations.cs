using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

internal sealed class NavigateOperation : UiOperationBase
{
    public NavigateOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.navigate";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
        {
            return OperationResult.Fail("ui.navigate requires a 'url' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            await page.GotoAsync(url);
            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}

internal sealed class ClickOperation : UiOperationBase
{
    public ClickOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.click";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.click requires a 'selector' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            await page.ClickAsync(selector, new PageClickOptions { Timeout = TimeoutMs(parameters) });
            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    internal static float? TimeoutMs(IReadOnlyDictionary<string, object?> parameters) =>
        parameters.TryGetValue("timeoutMs", out var value) && value is not null ? Convert.ToSingle(value) : null;
}

internal sealed class FillOperation : UiOperationBase
{
    public FillOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.fill";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.fill requires a 'selector' parameter");
        }

        if (!parameters.TryGetValue("value", out var valueObj) || valueObj is not string value)
        {
            return OperationResult.Fail("ui.fill requires a 'value' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            await page.FillAsync(selector, value, new PageFillOptions { Timeout = ClickOperation.TimeoutMs(parameters) });
            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}

internal sealed class WaitForOperation : UiOperationBase
{
    public WaitForOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.waitFor";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        var hasSelector = parameters.TryGetValue("selector", out var selectorObj) && selectorObj is string selector && !string.IsNullOrWhiteSpace(selector);
        var hasUrl = parameters.TryGetValue("url", out var urlObj) && urlObj is string url && !string.IsNullOrWhiteSpace(url);

        if (hasSelector && hasUrl)
        {
            return OperationResult.Fail("ui.waitFor takes exactly one wait target — a 'selector' or a 'url', not both");
        }

        if (!hasSelector && !hasUrl)
        {
            return OperationResult.Fail("ui.waitFor requires a 'selector' or a 'url' parameter");
        }

        // ui-adapter (spa-ui-adapter-ergonomics): wait for a client-side route change, so a case can
        // synchronize on a single-page-app navigation that fires no full page load. `url` is matched
        // as a substring, or as a glob when it contains '*' ('*' -> any run of characters).
        if (hasUrl)
        {
            var pattern = (string)urlObj!;
            try
            {
                var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
                await page.WaitForURLAsync(
                    current => UrlMatches(current, pattern),
                    new PageWaitForURLOptions { Timeout = ClickOperation.TimeoutMs(parameters) });
                return await UiOperationSupport.CompleteAsync(page, captures);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
                return OperationResult.Fail($"ui.waitFor timed out waiting for the URL to match '{pattern}'; last URL was '{page.Url}'");
            }
        }

        var selectorValue = (string)selectorObj!;
        var state = parameters.TryGetValue("state", out var stateObj) && stateObj is string stateName
            ? ParseState(stateName)
            : WaitForSelectorState.Visible;

        if (state is null)
        {
            return OperationResult.Fail($"ui.waitFor has an unknown 'state' value '{parameters["state"]}' (expected visible, hidden, attached, or detached)");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            await page.WaitForSelectorAsync(selectorValue, new PageWaitForSelectorOptions { State = state, Timeout = ClickOperation.TimeoutMs(parameters) });
            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    internal static bool UrlMatches(string current, string pattern)
    {
        if (!pattern.Contains('*'))
        {
            return current.Contains(pattern, StringComparison.Ordinal);
        }

        var regex = "^" + string.Join(".*", pattern.Split('*').Select(System.Text.RegularExpressions.Regex.Escape)) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(current, regex);
    }

    private static WaitForSelectorState? ParseState(string name) => name switch
    {
        "visible" => WaitForSelectorState.Visible,
        "hidden" => WaitForSelectorState.Hidden,
        "attached" => WaitForSelectorState.Attached,
        "detached" => WaitForSelectorState.Detached,
        _ => null,
    };
}

/// <summary>
/// ui-adapter (spa-ui-adapter-ergonomics): asserts an element's rendered text, so a case can check
/// *what* a component rendered, not just that it is present (which is <c>ui.assertVisible</c>).
/// Exactly one of <c>equals</c> / <c>contains</c> is required; its value has already had
/// <c>${VAR}</c> (load time) and <c>{{capture}}</c> (per run) substitution applied by the core.
/// </summary>
internal sealed class AssertTextOperation : UiOperationBase
{
    public AssertTextOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.assertText";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.assertText requires a 'selector' parameter");
        }

        var hasEquals = parameters.TryGetValue("equals", out var equalsObj) && equalsObj is string;
        var hasContains = parameters.TryGetValue("contains", out var containsObj) && containsObj is string;

        if (hasEquals == hasContains)
        {
            return OperationResult.Fail("ui.assertText requires exactly one of 'equals' or 'contains'");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            var actual = await page.InnerTextAsync(selector, new PageInnerTextOptions { Timeout = ClickOperation.TimeoutMs(parameters) });
            var trimmed = actual.Trim();

            if (hasEquals)
            {
                var expected = (string)equalsObj!;
                return trimmed == expected
                    ? await UiOperationSupport.CompleteAsync(page, captures)
                    : OperationResult.Fail($"element '{selector}' text was '{trimmed}', expected exactly '{expected}'");
            }

            var needle = (string)containsObj!;
            return trimmed.Contains(needle, StringComparison.Ordinal)
                ? await UiOperationSupport.CompleteAsync(page, captures)
                : OperationResult.Fail($"element '{selector}' text was '{trimmed}', expected it to contain '{needle}'");
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}

internal sealed class AssertVisibleOperation : UiOperationBase
{
    public AssertVisibleOperation(IBrowser browser, string? recordVideoDir = null) : base(browser, recordVideoDir) { }

    protected override string ActionName => "ui.assertVisible";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.assertVisible requires a 'selector' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, Browser, RecordVideoDir);
            if (!await page.IsVisibleAsync(selector))
            {
                return OperationResult.Fail($"element '{selector}' is not visible");
            }

            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Closes the run's browser context (and its pages). ui-session-video: when the context was
/// recording, resolves the finalized video and renames it to <c>&lt;caseId&gt;.webm</c> so a
/// consumer can find it by name.
/// </summary>
internal sealed class ClosePageCleanup : ICleanupOperation
{
    public async Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue(UiOperationSupport.ContextKey, out var existingContext) && existingContext is IBrowserContext browserContext)
        {
            // Capture video handles before close — Playwright only resolves the path after the
            // context closes. Prefer the run's stashed page; fall back to the context's open pages.
            var videos = new List<IVideo>();
            if (context.AdapterState.TryGetValue(UiOperationSupport.PageKey, out var pageObj) && pageObj is IPage stashedPage && stashedPage.Video is { } v)
            {
                videos.Add(v);
            }

            videos.AddRange(browserContext.Pages.Select(p => p.Video).Where(x => x is not null).Cast<IVideo>());

            await browserContext.CloseAsync();
            context.AdapterState.Remove(UiOperationSupport.ContextKey);
            context.AdapterState.Remove(UiOperationSupport.PageKey);

            await FinalizeVideosAsync(videos.Distinct().ToList(), context.Case.CaseId);
            return new CleanupResult(true);
        }

        if (context.AdapterState.TryGetValue(UiOperationSupport.PageKey, out var existing) && existing is IPage page)
        {
            await page.CloseAsync();
            context.AdapterState.Remove(UiOperationSupport.PageKey);
        }

        return new CleanupResult(true);
    }

    private static async Task FinalizeVideosAsync(IReadOnlyList<IVideo> videos, string caseId)
    {
        foreach (var video in videos)
        {
            try
            {
                // PathAsync() gives the GUID-named recording path (its directory is recordVideoDir);
                // SaveAsAsync waits for the recording to flush and writes it under our chosen name.
                var source = await video.PathAsync();
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                var target = Path.Combine(Path.GetDirectoryName(source)!, $"{Sanitize(caseId)}.webm");
                if (string.Equals(source, target, StringComparison.Ordinal))
                {
                    continue;
                }

                await video.SaveAsAsync(target);
                await video.DeleteAsync();
            }
            catch
            {
                // best-effort — a missing/failed video never fails cleanup.
            }
        }
    }

    private static string Sanitize(string caseId) =>
        new(caseId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());
}
