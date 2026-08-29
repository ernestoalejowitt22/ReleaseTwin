using Microsoft.Playwright;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.Ui;

/// <summary>
/// Seeds a cookie on the run's browser context before a later navigation, so a journey can drive an
/// app that gates access on a cookie (an E2E auth bypass, a feature toggle, a locale) entirely from
/// case-file data. Scope is exactly one of `url` (absolute) or `domain` (+ optional `path`).
/// </summary>
internal sealed class SetCookieOperation : UiOperationBase
{
    private readonly IBrowser _browser;
    public SetCookieOperation(IBrowser browser) => _browser = browser;

    protected override string ActionName => "ui.setCookie";

    protected override async Task<OperationResult> RunAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, IReadOnlyList<CaptureDeclaration> captures, CancellationToken cancellationToken)
    {
        if (!TryGetString(parameters, "name", out var name))
        {
            return OperationResult.Fail("ui.setCookie requires a 'name' parameter");
        }

        if (!parameters.TryGetValue("value", out var valueObj) || valueObj is null)
        {
            return OperationResult.Fail("ui.setCookie requires a 'value' parameter");
        }

        var value = valueObj.ToString() ?? string.Empty;

        var hasUrl = TryGetString(parameters, "url", out var url);
        var hasDomain = TryGetString(parameters, "domain", out var domain);

        if (hasUrl == hasDomain)
        {
            return OperationResult.Fail("ui.setCookie requires exactly one of 'url' or 'domain' (a domain may also set 'path')");
        }

        var cookie = new Cookie { Name = name, Value = value };

        if (hasUrl)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return OperationResult.Fail($"ui.setCookie 'url' must be an absolute URL, got '{url}'");
            }

            cookie.Url = url;
        }
        else
        {
            cookie.Domain = domain;
            cookie.Path = TryGetString(parameters, "path", out var path) ? path : "/";
        }

        if (TryGetBool(parameters, "secure", out var secure))
        {
            cookie.Secure = secure;
        }

        if (TryGetBool(parameters, "httpOnly", out var httpOnly))
        {
            cookie.HttpOnly = httpOnly;
        }

        if (TryGetString(parameters, "sameSite", out var sameSite))
        {
            cookie.SameSite = sameSite.ToLowerInvariant() switch
            {
                "strict" => SameSiteAttribute.Strict,
                "lax" => SameSiteAttribute.Lax,
                "none" => SameSiteAttribute.None,
                _ => null,
            };

            if (cookie.SameSite is null)
            {
                return OperationResult.Fail($"ui.setCookie 'sameSite' must be Strict, Lax, or None, got '{sameSite}'");
            }
        }

        if (parameters.TryGetValue("expires", out var expiresObj) && expiresObj is not null)
        {
            try
            {
                cookie.Expires = Convert.ToSingle(expiresObj);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return OperationResult.Fail("ui.setCookie 'expires' must be a unix timestamp in seconds");
            }
        }

        try
        {
            var browserContext = await UiOperationSupport.GetOrCreateContextAsync(context, _browser);
            await browserContext.AddCookiesAsync(new[] { cookie });
            return OperationResult.Pass($"set cookie '{name}'");
        }
        catch (Exception ex) when (ex is PlaywrightException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> parameters, string key, out string value)
    {
        if (parameters.TryGetValue(key, out var obj) && obj is string s && !string.IsNullOrWhiteSpace(s))
        {
            value = s;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetBool(IReadOnlyDictionary<string, object?> parameters, string key, out bool value)
    {
        value = false;
        if (!parameters.TryGetValue(key, out var obj) || obj is null)
        {
            return false;
        }

        switch (obj)
        {
            case bool b:
                value = b;
                return true;
            case string s when bool.TryParse(s, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }
}
