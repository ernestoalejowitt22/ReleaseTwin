using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

internal sealed class NavigateOperation : UiOperationBase
{
    private readonly IBrowser _browser;
    public NavigateOperation(IBrowser browser) => _browser = browser;

    protected override string ActionName => "ui.navigate";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
        {
            return OperationResult.Fail("ui.navigate requires a 'url' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, _browser);
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
    private readonly IBrowser _browser;
    public ClickOperation(IBrowser browser) => _browser = browser;

    protected override string ActionName => "ui.click";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.click requires a 'selector' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, _browser);
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
    private readonly IBrowser _browser;
    public FillOperation(IBrowser browser) => _browser = browser;

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
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, _browser);
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
    private readonly IBrowser _browser;
    public WaitForOperation(IBrowser browser) => _browser = browser;

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
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, _browser);
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
    private readonly IBrowser _browser;
    public AssertVisibleOperation(IBrowser browser) => _browser = browser;

    protected override string ActionName => "ui.assertVisible";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector || string.IsNullOrWhiteSpace(selector))
        {
            return OperationResult.Fail("ui.assertVisible requires a 'selector' parameter");
        }

        try
        {
            var page = await UiOperationSupport.GetOrCreatePageAsync(context, _browser);
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

internal sealed class ClosePageCleanup : ICleanupOperation
{
    public async Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("ui.page", out var existing) && existing is IPage page)
        {
            await page.CloseAsync();
            context.AdapterState.Remove("ui.page");
        }

        return new CleanupResult(true);
    }
}
