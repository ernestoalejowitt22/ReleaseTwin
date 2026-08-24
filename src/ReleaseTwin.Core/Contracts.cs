namespace ReleaseTwin.Core;

public sealed class CaseExecutionContext
{
    public required TestCase Case { get; init; }

    /// <summary>Adapter-owned state bag. Core never reads or writes specific keys here.</summary>
    public IDictionary<string, object?> AdapterState { get; } = new Dictionary<string, object?>();
}

public enum PrerequisiteStatus
{
    Satisfied,
    NotSatisfied,

    /// <summary>The check could not be completed (e.g. an unreachable dependency) — distinct from a confirmed NotSatisfied.</summary>
    Inconclusive,
}

public sealed record PrerequisiteResult(PrerequisiteStatus Status, string? Detail = null)
{
    public static PrerequisiteResult Satisfied(string? detail = null) => new(PrerequisiteStatus.Satisfied, detail);
    public static PrerequisiteResult NotSatisfied(string? detail = null) => new(PrerequisiteStatus.NotSatisfied, detail);
    public static PrerequisiteResult Inconclusive(string? detail = null) => new(PrerequisiteStatus.Inconclusive, detail);
}

public interface IPrerequisiteCheck
{
    Task<PrerequisiteResult> EvaluateAsync(CaseExecutionContext context, CancellationToken cancellationToken);
}

public sealed record OperationResult(bool Succeeded, string? Detail = null)
{
    public static OperationResult Pass(string? detail = null) => new(true, detail);
    public static OperationResult Fail(string? detail = null) => new(false, detail);
}

public interface IOperation
{
    Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken);
}

public sealed record CleanupResult(bool Succeeded, string? Detail = null);

public interface ICleanupOperation
{
    Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>Resolves a declared name to its adapter-contributed implementation. Implemented by the adapter SDK's composition root, not by Core.</summary>
public interface IOperationCatalog
{
    bool TryGet(string name, out IOperation operation);
}

public interface IPrerequisiteCatalog
{
    bool TryGet(string name, out IPrerequisiteCheck check);
}

public interface ICleanupCatalog
{
    bool TryGet(string name, out ICleanupOperation operation);
}

public sealed class UnknownReferenceException : Exception
{
    public UnknownReferenceException(string message) : base(message)
    {
    }
}
