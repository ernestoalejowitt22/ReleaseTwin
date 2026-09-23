using System.Text.RegularExpressions;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>
/// Converts a case file's <c>pipeline:</c> list into core pipeline entries and enforces the
/// journey-branching load-time rules: <c>kind: choice</c> is the only entry kind besides a plain
/// step, a branch holds plain steps only (no nested choice), a condition names a known operator,
/// and a capture reference resolves within the referencing entry's own static scope.
/// </summary>
internal static class PipelineEntryLoader
{
    private const string ChoiceKind = "choice";
    private static readonly Regex CaptureReference = new(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);
    private static readonly Regex CaptureName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static IReadOnlyList<IPipelineEntry> Load(
        string fileName,
        List<PipelineStepDto>? dtos,
        Func<object?, IReadOnlyDictionary<string, object?>?> convertParameters)
    {
        var entries = new List<IPipelineEntry>();
        foreach (var dto in dtos ?? new List<PipelineStepDto>())
        {
            if (dto.Kind is null)
            {
                entries.Add(LoadStep(fileName, dto, convertParameters, where: "a pipeline step"));
                continue;
            }

            if (!string.Equals(dto.Kind, ChoiceKind, StringComparison.Ordinal))
            {
                throw new CaseFileException(fileName, $"unknown pipeline entry kind '{dto.Kind}' (the only supported kind is 'choice')");
            }

            entries.Add(LoadChoice(fileName, dto, convertParameters));
        }

        ValidateScope(fileName, entries);
        return entries;
    }

    private static ChoiceStep LoadChoice(string fileName, PipelineStepDto dto, Func<object?, IReadOnlyDictionary<string, object?>?> convertParameters)
    {
        if (dto.Operation is not null || dto.With is not null || dto.Capture is not null)
        {
            throw new CaseFileException(fileName, "a 'kind: choice' entry cannot declare 'operation', 'with', or 'capture' — put steps in its 'then'/'else' branches");
        }

        if (dto.When is null)
        {
            throw new CaseFileException(fileName, "a 'kind: choice' entry is missing 'when'");
        }

        var condition = LoadCondition(fileName, dto.When, "a choice's 'when'");
        var then = LoadBranch(fileName, dto.Then, "then", convertParameters);
        var @else = LoadBranch(fileName, dto.Else, "else", convertParameters);
        return new ChoiceStep(condition, then, @else);
    }

    private static List<PipelineStep> LoadBranch(string fileName, List<PipelineStepDto>? dtos, string branch, Func<object?, IReadOnlyDictionary<string, object?>?> convertParameters)
    {
        var steps = new List<PipelineStep>();
        foreach (var dto in dtos ?? new List<PipelineStepDto>())
        {
            if (dto.Kind is not null)
            {
                throw string.Equals(dto.Kind, ChoiceKind, StringComparison.Ordinal)
                    ? new CaseFileException(fileName, $"a choice cannot be nested inside another choice's '{branch}' branch — branches hold plain steps only")
                    : new CaseFileException(fileName, $"unknown pipeline entry kind '{dto.Kind}' in a choice's '{branch}' branch");
            }

            steps.Add(LoadStep(fileName, dto, convertParameters, where: $"a step in a choice's '{branch}' branch"));
        }

        return steps;
    }

    private static PipelineStep LoadStep(string fileName, PipelineStepDto dto, Func<object?, IReadOnlyDictionary<string, object?>?> convertParameters, string where)
    {
        if (dto.Then is not null || dto.Else is not null)
        {
            throw new CaseFileException(fileName, $"{where} declares 'then'/'else', which are only valid on a 'kind: choice' entry");
        }

        if (string.IsNullOrWhiteSpace(dto.Operation))
        {
            throw new CaseFileException(fileName, $"{where} is missing 'operation'");
        }

        var parameters = convertParameters(dto.With);

        var captures = (dto.Capture ?? new List<CaptureDto>())
            .Select(c =>
            {
                if (string.IsNullOrWhiteSpace(c.Name))
                {
                    throw new CaseFileException(fileName, "a pipeline step's capture is missing 'name'");
                }

                if (string.IsNullOrWhiteSpace(c.From))
                {
                    throw new CaseFileException(fileName, "a pipeline step's capture is missing 'from'");
                }

                return new CaptureDeclaration(c.Name, c.From);
            })
            .ToList();

        var guard = dto.When is null ? null : LoadCondition(fileName, dto.When, $"the 'when' guard of step '{dto.Operation}'");

        return new PipelineStep(dto.Operation, With: parameters, Capture: captures.Count > 0 ? captures : null, When: guard);
    }

    private static Condition LoadCondition(string fileName, ConditionDto dto, string where)
    {
        var rawRef = dto.Ref?.Trim();
        if (string.IsNullOrEmpty(rawRef))
        {
            throw new CaseFileException(fileName, $"{where} is missing 'ref'");
        }

        // Accept both the bare capture name and the `{{name}}` reference form.
        var name = rawRef.StartsWith("{{", StringComparison.Ordinal) && rawRef.EndsWith("}}", StringComparison.Ordinal)
            ? rawRef[2..^2].Trim()
            : rawRef;
        if (!CaptureName.IsMatch(name))
        {
            throw new CaseFileException(fileName, $"{where} has an invalid 'ref' '{dto.Ref}' — expected a capture name");
        }

        var op = dto.Op?.Trim() switch
        {
            "==" => ConditionOperator.Equals,
            "!=" => ConditionOperator.NotEquals,
            "exists" => ConditionOperator.Exists,
            "!exists" => ConditionOperator.NotExists,
            null or "" => throw new CaseFileException(fileName, $"{where} is missing 'op'"),
            var other => throw new CaseFileException(fileName, $"{where} has unsupported 'op' '{other}' (supported: ==, !=, exists, !exists)"),
        };

        if (op is ConditionOperator.Equals or ConditionOperator.NotEquals && dto.Value is null)
        {
            throw new CaseFileException(fileName, $"{where} uses '{dto.Op}' but is missing 'value'");
        }

        return new Condition(name, op, op is ConditionOperator.Exists or ConditionOperator.NotExists ? null : dto.Value);
    }

    /// <summary>
    /// Static (not run-time) scope check: a condition's ref must be captured by an entry earlier in
    /// the same branch or earlier than the enclosing choice. A <c>{{name}}</c> parameter reference is
    /// rejected when <c>name</c> is captured somewhere in the case but only outside the referencing
    /// step's scope (a sibling branch, or inside a branch referenced after the join). A reference to a
    /// name captured nowhere keeps its existing run-time <c>missing-capture</c> behavior, so linear
    /// case files that loaded before this rule still load unchanged.
    /// </summary>
    private static void ValidateScope(string fileName, IReadOnlyList<IPipelineEntry> entries)
    {
        var declaredAnywhere = entries.FlattenSteps()
            .SelectMany(s => s.Captures)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            switch (entry)
            {
                case PipelineStep step:
                    CheckStep(fileName, step, visible, declaredAnywhere);
                    break;
                case ChoiceStep choice:
                    CheckConditionRef(fileName, choice.When, visible, "a choice's 'when'");
                    foreach (var branch in new[] { choice.Then, choice.Else })
                    {
                        var branchVisible = new HashSet<string>(visible, StringComparer.Ordinal);
                        foreach (var step in branch)
                        {
                            CheckStep(fileName, step, branchVisible, declaredAnywhere);
                        }
                    }

                    // Branch-local captures are deliberately NOT added to `visible`: after the join
                    // the branch that produced them may not have run.
                    break;
            }
        }
    }

    private static void CheckStep(string fileName, PipelineStep step, HashSet<string> visible, HashSet<string> declaredAnywhere)
    {
        if (step.When is { } guard)
        {
            CheckConditionRef(fileName, guard, visible, $"the 'when' guard of step '{step.OperationName}'");
        }

        foreach (var name in ReferencedCaptures(step.Parameters))
        {
            if (!visible.Contains(name) && declaredAnywhere.Contains(name))
            {
                throw new CaseFileException(fileName,
                    $"step '{step.OperationName}' references capture '{name}', which is not in scope here — it is only captured inside a choice branch that does not enclose this step; reorder, or capture it before the branch");
            }
        }

        foreach (var capture in step.Captures)
        {
            visible.Add(capture.Name);
        }
    }

    private static void CheckConditionRef(string fileName, Condition condition, HashSet<string> visible, string where)
    {
        if (!visible.Contains(condition.Ref))
        {
            throw new CaseFileException(fileName,
                $"{where} references capture '{condition.Ref}', which is not in scope — it must be captured by an earlier step in the same branch or before the enclosing choice");
        }
    }

    private static IEnumerable<string> ReferencedCaptures(object? value)
    {
        switch (value)
        {
            case string s:
                foreach (Match match in CaptureReference.Matches(s))
                {
                    yield return match.Groups[1].Value;
                }

                break;
            case IReadOnlyDictionary<string, object?> dict:
                foreach (var nested in dict.Values.SelectMany(ReferencedCaptures))
                {
                    yield return nested;
                }

                break;
            case IEnumerable<object?> list:
                foreach (var nested in list.SelectMany(ReferencedCaptures))
                {
                    yield return nested;
                }

                break;
        }
    }
}
