using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// evidence-capture: the adapter-defined evidence a ui.* step attaches — an ordered action log and,
/// where captured, a screenshot handle. Screenshot bytes are written to a temp file by the adapter
/// and referenced by path here (out-of-band of the core), for the CLI to pick up, redact, and upload.
/// </summary>
public sealed record UiStepEvidence(
    string Action,
    IReadOnlyDictionary<string, string?> Parameters,
    string? ScreenshotPath,
    string? ScreenshotSelector,
    bool ValueIsProtected = false);

/// <summary>
/// Base for ui.* operations: owns the per-step evidence buffer and the drain hook, so each concrete
/// operation only records its own action. Screenshot capture is best-effort and never fails a step.
/// </summary>
internal abstract class UiOperationBase : IOperation, IEvidenceEmittingOperation
{
    private readonly object _lock = new();
    private UiStepEvidence? _pending;
    private bool _captureEnabled;

    protected abstract Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken);

    protected abstract string ActionName { get; }

    public async Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _pending = null;
            _captureEnabled = context.CaptureEvidence;
        }

        var result = await RunAsync(context, parameters, captures, cancellationToken);

        if (_captureEnabled)
        {
            await RecordAsync(context, parameters);
        }

        return result;
    }

    private async Task RecordAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters)
    {
        var recorded = new Dictionary<string, string?>();
        foreach (var (key, value) in parameters)
        {
            recorded[key] = value?.ToString();
        }

        context.AdapterState.TryGetValue(UiOperationSupport.PageKey, out var pageObj);
        var page = pageObj as IPage;

        // evidence-capture: a value typed into a password field is masked here, before the value ever
        // leaves the adapter, so no per-case rule and no later allowlist entry can re-expose it.
        var valueIsProtected = false;
        if (page is not null && !page.IsClosed && recorded.ContainsKey("value")
            && parameters.TryGetValue("selector", out var selObj) && selObj is string selector && !string.IsNullOrWhiteSpace(selector))
        {
            try
            {
                var inputType = await page.EvalOnSelectorAsync<string?>(selector, "el => el && el.type ? String(el.type) : null");
                if (string.Equals(inputType, "password", StringComparison.OrdinalIgnoreCase))
                {
                    recorded["value"] = "«password»";
                    valueIsProtected = true;
                }
            }
            catch
            {
                // best-effort — if we can't inspect the element, the CLI redactor still masks a
                // ui.* step's `value` by default (just re-includable via an allowlist entry).
            }
        }

        string? screenshotPath = null;
        try
        {
            if (page is not null && !page.IsClosed)
            {
                screenshotPath = Path.Combine(Path.GetTempPath(), $"rt-evidence-{Guid.NewGuid():N}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
            }
        }
        catch
        {
            screenshotPath = null; // best-effort; never fail a step for a screenshot
        }

        lock (_lock)
        {
            _pending = new UiStepEvidence(ActionName, recorded, screenshotPath, null, valueIsProtected);
        }
    }

    public EvidenceContribution? DrainEvidence()
    {
        lock (_lock)
        {
            var evidence = _pending;
            _pending = null;
            return evidence is null ? null : new EvidenceContribution(Assertion: null, Adapter: evidence);
        }
    }
}
