using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ReleaseTwin.Cli.Evidence;

/// <summary>
/// local-evidence-artifacts: writes a case's already-redacted <see cref="RedactionResult"/> to a local
/// directory, independent of any hosted upload. Layout is one subdirectory per case id:
/// <c>&lt;dir&gt;/&lt;case-id&gt;/evidence.json</c> plus <c>&lt;dir&gt;/&lt;case-id&gt;/&lt;screenshot-id&gt;.png</c>
/// for each redacted screenshot. Re-running the same case id overwrites its prior files.
/// </summary>
public static class LocalEvidenceWriter
{
    public static async Task WriteAsync(string evidenceDir, string caseId, RedactionResult evidence, CancellationToken cancellationToken)
    {
        var caseDir = Path.Combine(evidenceDir, caseId);
        Directory.CreateDirectory(caseDir);

        var documentJson = JObject.FromObject(evidence.Document, CamelCase).ToString(Formatting.Indented);
        await File.WriteAllTextAsync(Path.Combine(caseDir, "evidence.json"), documentJson, cancellationToken);

        foreach (var screenshot in evidence.Screenshots)
        {
            await File.WriteAllBytesAsync(Path.Combine(caseDir, $"{screenshot.Id}.png"), screenshot.PngBytes, cancellationToken);
        }
    }

    private static readonly JsonSerializer CamelCase = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
    });
}
