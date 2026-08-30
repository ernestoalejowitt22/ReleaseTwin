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
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.waitFor requires a 'selector' parameter");
        }

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
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { State = state, Timeout = ClickOperation.TimeoutMs(parameters) });
            return await UiOperationSupport.CompleteAsync(page, captures);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return OperationResult.Fail(ex.Message);
        }
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
