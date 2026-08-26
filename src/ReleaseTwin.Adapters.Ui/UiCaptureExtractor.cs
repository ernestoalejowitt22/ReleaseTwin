using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// Shared value-capture extraction for any UI operation, per value-capture: a UI step MAY declare a
/// capture of a value observed on the page (currently: an element's text content).
/// </summary>
internal static class UiCaptureExtractor
{
    public static async Task<(bool Success, IReadOnlyDictionary<string, string> Captures, string? Error)> TryExtractAllAsync(
        IPage page, IReadOnlyList<CaptureDeclaration> captures)
    {
        var values = new Dictionary<string, string>();
        foreach (var capture in captures)
        {
            var separatorIndex = capture.From.IndexOf(':');
            if (separatorIndex < 0)
            {
                return (false, values, $"capture '{capture.Name}' failed: source '{capture.From}' must be of the form 'text:<selector>'");
            }

            var kind = capture.From[..separatorIndex];
            var selector = capture.From[(separatorIndex + 1)..];

            if (kind != "text")
            {
                return (false, values, $"capture '{capture.Name}' failed: unknown capture source kind '{kind}' (expected 'text')");
            }

            try
            {
                values[capture.Name] = await page.InnerTextAsync(selector);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return (false, values, $"capture '{capture.Name}' failed: {ex.Message}");
            }
        }

        return (true, values, null);
    }
}
