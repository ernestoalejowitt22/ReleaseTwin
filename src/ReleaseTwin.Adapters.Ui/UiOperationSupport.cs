using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// Shared page-lifecycle and capture-completion logic for every ui.* operation, so each operation
/// class only implements its own action.
/// </summary>
internal static class UiOperationSupport
{
    /// <summary>
    /// One page per case run, lazily created on first use and stashed on the run-scoped
    /// <see cref="CaseExecutionContext.AdapterState"/> — mirrors how http.request stashes its last
    /// response there today. Closed by the case author's own declared `ui.closePage` cleanup step.
    /// </summary>
    public static async Task<IPage> GetOrCreatePageAsync(CaseExecutionContext context, IBrowser browser)
    {
        if (context.AdapterState.TryGetValue("ui.page", out var existing) && existing is IPage page)
        {
            return page;
        }

        var newPage = await browser.NewPageAsync();
        context.AdapterState["ui.page"] = newPage;
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
