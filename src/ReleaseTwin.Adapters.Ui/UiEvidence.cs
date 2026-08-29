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
    string? ScreenshotSelector);

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

        string? screenshotPath = null;
        try
        {
            if (context.AdapterState.TryGetValue("ui.page", out var pageObj) && pageObj is IPage page && !page.IsClosed)
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
            _pending = new UiStepEvidence(ActionName, recorded, screenshotPath, null);
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
