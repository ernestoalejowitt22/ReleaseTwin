using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReleaseTwin.Cli;

/// <summary>Raised when <c>releasetwin.yaml</c> is present but unusable — a hard startup error.</summary>
public sealed class ReleaseTwinConfigException(string message) : Exception(message);

/// <summary>
/// config-driven-adapter-selection: the optional <c>releasetwin.yaml</c> at the project root.
/// Names <em>which</em> adapters a project uses; never carries credentials (those still resolve
/// only from the environment or the hosted <c>adapter-credentials</c> capability).
/// </summary>
public sealed class ReleaseTwinConfig
{
    /// <summary>The four adapters the CLI knows how to compose.</summary>
    public static readonly IReadOnlyList<string> KnownAdapters = new[] { "http", "azure-devops", "launchdarkly", "ui" };

    /// <summary>
    /// The declared adapter list, lower-cased and trimmed, or <c>null</c> when there is no file or
    /// no <c>adapters:</c> key — in which case the CLI keeps its pre-config auto-detection.
    /// </summary>
    public IReadOnlyList<string>? Adapters { get; private init; }

    /// <summary>
    /// True when adapter <paramref name="name"/> should be considered: always when there is no
    /// declared list, otherwise only if the list contains it. <c>http</c> is always considered.
    /// </summary>
    public bool Considers(string name) =>
        string.Equals(name, "http", StringComparison.OrdinalIgnoreCase)
        || Adapters is null
        || Adapters.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the list explicitly names <paramref name="name"/> (i.e. the user asked for it).</summary>
    public bool Requires(string name) =>
        Adapters is not null && Adapters.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Load <c>releasetwin.yaml</c> for a run whose cases live in <paramref name="casesDirectory"/>.
    /// Looks in that directory's parent, then the current directory. Absent → an empty config
    /// (auto-detection). Malformed / unknown adapter name → <see cref="ReleaseTwinConfigException"/>.
    /// </summary>
    public static ReleaseTwinConfig LoadFor(string casesDirectory)
    {
        foreach (var root in CandidateRoots(casesDirectory))
        {
            var path = Path.Combine(root, "releasetwin.yaml");
            if (File.Exists(path))
            {
                return Parse(File.ReadAllText(path), path);
            }
        }

        return new ReleaseTwinConfig();
    }

    /// <summary>Parse config <paramref name="yaml"/> (from <paramref name="path"/>, used in errors).</summary>
    public static ReleaseTwinConfig Parse(string yaml, string path)
    {
        Dto? dto;
        try
        {
            dto = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<Dto?>(yaml);
        }
        catch (YamlException ex)
        {
            throw new ReleaseTwinConfigException($"{path}: not valid YAML — {ex.Message}");
        }

        if (dto?.Adapters is null)
        {
            return new ReleaseTwinConfig();
        }

        var names = dto.Adapters
            .Select(a => (a ?? string.Empty).Trim().ToLowerInvariant())
            .Where(a => a.Length > 0)
            .ToList();

        var unknown = names.Where(a => !KnownAdapters.Contains(a)).ToList();
        if (unknown.Count > 0)
        {
            throw new ReleaseTwinConfigException(
                $"{path}: unknown adapter(s) {string.Join(", ", unknown)}. Known: {string.Join(", ", KnownAdapters)}.");
        }

        return new ReleaseTwinConfig { Adapters = names };
    }

    private static IEnumerable<string> CandidateRoots(string casesDirectory)
    {
        var parent = Directory.GetParent(Path.GetFullPath(casesDirectory))?.FullName;
        if (parent is not null)
        {
            yield return parent;
        }

        yield return Directory.GetCurrentDirectory();
    }

    private sealed class Dto
    {
        public List<string?>? Adapters { get; set; }
    }
}
