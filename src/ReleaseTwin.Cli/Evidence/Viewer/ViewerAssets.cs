using System.Reflection;

namespace ReleaseTwin.Cli.Evidence.Viewer;

/// <summary>
/// local-evidence-viewer: the viewer's stylesheet and script, compiled into the assembly as embedded
/// resources the same way <see cref="Scaffolding.ScaffoldWriter"/> reads its templates (design D3).
/// Nothing to download, nothing to build — both published forms of the CLI carry them already.
/// </summary>
public static class ViewerAssets
{
    public const string CssFileName = "viewer.css";
    public const string JsFileName = "viewer.js";

    public static string Css => Read(CssFileName);

    public static string Js => Read(JsFileName);

    private static string Read(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = $"ReleaseTwin.Cli.Evidence.Viewer.Assets.{name}";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded viewer asset '{resource}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
