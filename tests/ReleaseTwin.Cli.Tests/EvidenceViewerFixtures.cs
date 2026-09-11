namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// local-evidence-viewer: the shared evidence-document fixtures, and a scratch evidence directory in
/// the layout <c>LocalEvidenceWriter</c> writes.
/// </summary>
internal static class EvidenceViewerFixtures
{
    public const string OrdinaryFileName = "evidence-document.json";
    public const string FlagProofFileName = "evidence-document-flag-proof.json";

    public static string PathTo(string fileName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    public static string Read(string fileName) => File.ReadAllText(PathTo(fileName));

    /// <summary>A one-pixel PNG, so a screenshot file on disk is a real image rather than a stub.</summary>
    public static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}

/// <summary>A temporary directory that deletes itself, so tests never touch the repo or leave litter.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rt-viewer-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes <c>&lt;case-id&gt;/evidence.json</c>, the layout the viewer reads.</summary>
    public string WriteCase(string caseDirectoryName, string documentJson)
    {
        var dir = System.IO.Path.Combine(Path, caseDirectoryName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(System.IO.Path.Combine(dir, "evidence.json"), documentJson);
        return dir;
    }

    public void WriteScreenshot(string caseDirectoryName, string screenshotId)
    {
        var dir = System.IO.Path.Combine(Path, caseDirectoryName);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(System.IO.Path.Combine(dir, screenshotId + ".png"), EvidenceViewerFixtures.OnePixelPng);
    }

    public string Combine(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
