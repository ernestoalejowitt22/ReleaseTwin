using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// Shared page-lifecycle and capture-completion logic for every ui.* operation, so each operation
/// class only implements its own action.
/// </summary>
internal static class UiOperationSupport
{
    internal const string ContextKey = "ui.context";
    internal const string PageKey = "ui.page";

    /// <summary>
    /// One <see cref="IBrowserContext"/> per case run, lazily created on first use and stashed on the
    /// run-scoped <see cref="CaseExecutionContext.AdapterState"/> — so cookies/storage are isolated
    /// per run and a cookie seeded by one step (see <c>ui.setCookie</c>) is visible to every later
    /// step. Closed by the case author's own declared `ui.closePage` cleanup step.
    /// </summary>
    public static async Task<IBrowserContext> GetOrCreateContextAsync(CaseExecutionContext context, IBrowser browser)
    {
        if (context.AdapterState.TryGetValue(ContextKey, out var existingContext) && existingContext is IBrowserContext browserContext)
        {
            return browserContext;
        }

        var newContext = await browser.NewContextAsync();
        context.AdapterState[ContextKey] = newContext;
        return newContext;
    }

    /// <summary>
    /// One page per case run, opened from the run's browser context on first use and stashed on the
    /// run-scoped <see cref="CaseExecutionContext.AdapterState"/>.
    /// </summary>
    public static async Task<IPage> GetOrCreatePageAsync(CaseExecutionContext context, IBrowser browser)
    {
        if (context.AdapterState.TryGetValue(PageKey, out var existing) && existing is IPage page)
        {
            return page;
        }

        var browserContext = await GetOrCreateContextAsync(context, browser);
        var newPage = await browserContext.NewPageAsync();
        context.AdapterState[PageKey] = newPage;
        return newPage;
    }

    public static async Task<OperationResult> CompleteAsync(IPage page, IReadOnlyList<CaptureDeclaration> captures)
    {
        if (captures.Count == 0)
        {
            return OperationResult.Pass();
        }

        var (success, values, error) = await UiCaptureExtractor.TryExtractAllAsync(page, captures);
        return success ? OperationResult.Pass(captures: values) : OperationResult.Fail(error);
    }
}
