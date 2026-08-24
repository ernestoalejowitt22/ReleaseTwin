using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Core;

namespace ReleaseTwin.Adapters.ToyFile;

/// <summary>
/// A deliberately different-shaped toy adapter: file-system based, no HTTP client, no auth token,
/// no request/response model. Exists specifically so the two toy adapters do not share an implicit
/// assumption (e.g. "every adapter has an HTTP client") that a real adapter might violate.
/// </summary>
public sealed class ToyFileAdapter : IAdapterModule
{
    private readonly ToyFileStore _store;

    public ToyFileAdapter(string workingDirectory)
    {
        _store = new ToyFileStore(workingDirectory);
    }

    public string Name => "toy-file";

    public void Register(IAdapterRegistrationBuilder builder)
    {
        builder
            .AddPrerequisite("toyfile.workingDirectoryExists", new WorkingDirectoryExistsCheck(_store))
            .AddOperation("toyfile.writeFile", new WriteFileOperation(_store))
            .AddOperation("toyfile.readFile", new ReadFileOperation(_store))
            .AddCleanup("toyfile.deleteFile", new DeleteFileCleanup(_store))
            .AddCapability("filesystem:toy");
    }
}

/// <summary>A minimal file-system abstraction, isolated to one working directory.</summary>
public sealed class ToyFileStore
{
    private readonly string _workingDirectory;

    public ToyFileStore(string workingDirectory) => _workingDirectory = workingDirectory;

    public bool WorkingDirectoryExists() => Directory.Exists(_workingDirectory);

    public string Write(string fileName, string content)
    {
        var path = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public bool TryRead(string path, out string content)
    {
        if (File.Exists(path))
        {
            content = File.ReadAllText(path);
            return true;
        }

        content = string.Empty;
        return false;
    }

    public bool Delete(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        File.Delete(path);
        return true;
    }
}

internal sealed class WorkingDirectoryExistsCheck : IPrerequisiteCheck
{
    private readonly ToyFileStore _store;
    public WorkingDirectoryExistsCheck(ToyFileStore store) => _store = store;

    public Task<PrerequisiteResult> EvaluateAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        var exists = _store.WorkingDirectoryExists();
        return Task.FromResult(exists
            ? PrerequisiteResult.Satisfied()
            : PrerequisiteResult.NotSatisfied("working directory does not exist"));
    }
}

internal sealed class WriteFileOperation : IOperation
{
    private readonly ToyFileStore _store;
    public WriteFileOperation(ToyFileStore store) => _store = store;

    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var path = _store.Write($"{context.Case.CaseId}.txt", "release-proof");
        context.AdapterState["toyfile.path"] = path;
        return Task.FromResult(OperationResult.Pass(path));
    }
}

internal sealed class ReadFileOperation : IOperation
{
    private readonly ToyFileStore _store;
    public ReadFileOperation(ToyFileStore store) => _store = store;

    public Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("toyfile.path", out var pathObj) && pathObj is string path
            && _store.TryRead(path, out var content) && content == "release-proof")
        {
            return Task.FromResult(OperationResult.Pass(content));
        }

        return Task.FromResult(OperationResult.Fail("file content mismatch"));
    }
}

internal sealed class DeleteFileCleanup : ICleanupOperation
{
    private readonly ToyFileStore _store;
    public DeleteFileCleanup(ToyFileStore store) => _store = store;

    public Task<CleanupResult> ExecuteAsync(CaseExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.AdapterState.TryGetValue("toyfile.path", out var pathObj) && pathObj is string path)
        {
            return Task.FromResult(new CleanupResult(_store.Delete(path)));
        }

        return Task.FromResult(new CleanupResult(true));
    }
}
