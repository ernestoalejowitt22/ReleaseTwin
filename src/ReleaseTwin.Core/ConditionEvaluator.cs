namespace ReleaseTwin.Core;

/// <summary>
/// journey-branching: evaluates a <see cref="Condition"/> against the values captured so far in a
/// run. A capture that is absent (never declared, or declared by a step that was guarded off, sat in
/// an untaken branch, or failed before capturing) is treated as absent rather than as an error:
/// <c>==</c> is false, <c>!=</c> is true, <c>exists</c> is false, <c>!exists</c> is true. Comparison
/// is ordinal string equality — the core does not interpret captured values.
/// </summary>
public static class ConditionEvaluator
{
    public static bool Evaluate(Condition condition, IReadOnlyDictionary<string, string> captures)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(captures);

        var present = captures.TryGetValue(condition.Ref, out var actual);
        return condition.Operator switch
        {
            ConditionOperator.Equals => present && string.Equals(actual, condition.Value ?? string.Empty, StringComparison.Ordinal),
            ConditionOperator.NotEquals => !present || !string.Equals(actual, condition.Value ?? string.Empty, StringComparison.Ordinal),
            ConditionOperator.Exists => present && !string.IsNullOrEmpty(actual),
            ConditionOperator.NotExists => !present || string.IsNullOrEmpty(actual),
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition.Operator, "Unknown condition operator"),
        };
    }
}
