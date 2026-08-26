using System.Text.RegularExpressions;

namespace ReleaseTwin.Core;

/// <summary>
/// Thrown when a step's parameters reference a capture name no earlier step in this run has
/// produced (not declared, declared by a step that hasn't run yet, or declared by a step that
/// failed before capturing). Per value-capture's requirement, this must fail the case clearly
/// rather than substitute a blank or literal placeholder.
/// </summary>
public sealed class MissingCaptureException : Exception
{
    public string CaptureName { get; }

    public MissingCaptureException(string captureName)
        : base($"reference to capture '{captureName}' that no earlier step in this run has captured")
    {
        CaptureName = captureName;
    }
}

/// <summary>
/// Resolves `{{captureName}}` references in a step's parameters against values captured by
/// earlier steps in the same run. Distinct from case-loading's `${VAR_NAME}` environment-variable
/// interpolation, which resolves once at load time — this resolves at pipeline-execution time,
/// immediately before the referencing step runs, since a captured value doesn't exist until an
/// earlier step has actually executed.
/// </summary>
internal static class CaptureReferenceResolver
{
    private static readonly Regex CapturePattern = new(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, object?> Resolve(
        IReadOnlyDictionary<string, object?> parameters, IReadOnlyDictionary<string, string> captures)
    {
        return parameters.ToDictionary(kv => kv.Key, kv => ResolveValue(kv.Value, captures));
    }

    private static object? ResolveValue(object? value, IReadOnlyDictionary<string, string> captures)
    {
        switch (value)
        {
            case string s:
                return CapturePattern.Replace(s, match =>
                {
                    var name = match.Groups[1].Value;
                    if (!captures.TryGetValue(name, out var resolved))
                    {
                        throw new MissingCaptureException(name);
                    }

                    return resolved;
                });
            case Dictionary<string, object?> dict:
                return dict.ToDictionary(kv => kv.Key, kv => ResolveValue(kv.Value, captures));
            case List<object?> list:
                return list.Select(v => ResolveValue(v, captures)).ToList();
            default:
                return value;
        }
    }
}
