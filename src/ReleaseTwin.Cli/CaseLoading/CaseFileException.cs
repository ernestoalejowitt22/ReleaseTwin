namespace ReleaseTwin.Cli.CaseLoading;

public sealed class CaseFileException : Exception
{
    public CaseFileException(string fileName, string problem)
        : base($"{fileName}: {problem}")
    {
    }
}
