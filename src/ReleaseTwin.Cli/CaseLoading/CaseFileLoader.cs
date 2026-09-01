using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ReleaseTwin.Core;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReleaseTwin.Cli.CaseLoading;

/// <summary>
/// Loads every *.yaml/*.yml case file in a directory into ReleaseTwin.Core.TestCase, per
/// design.md D1 (trimmed case-file format) and D3 (fixture root + path containment).
/// </summary>
public sealed class CaseFileLoader
{
    private static readonly Regex EnvVarPattern = new(@"\$\{([A-Z0-9_]+)\}", RegexOptions.Compiled);

    private readonly string? _casesDirectory;
    private readonly string _fixturesRoot;
    private readonly IDeserializer _deserializer;
    private readonly Func<string, string?> _resolveEnvironmentVariable;

    /// <summary>
    /// hosted-project-secrets: <paramref name="resolveEnvironmentVariable"/> lets a caller (CliRunner)
    /// substitute a lookup that also falls back to hosted-stored project secrets, without this loader
    /// knowing anything about that source — defaults to today's exact live-environment behavior
    /// (<see cref="Environment.GetEnvironmentVariable(string)"/>) when not supplied, so every existing
    /// call site is unaffected unless it opts in.
    /// </summary>
    public CaseFileLoader(string casesDirectory, string? fixturesRoot = null, Func<string, string?>? resolveEnvironmentVariable = null)
    {
        _casesDirectory = casesDirectory;
        _fixturesRoot = fixturesRoot ?? Path.Combine(casesDirectory, "..", "fixtures");
        _resolveEnvironmentVariable = resolveEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// For parsing YAML from a source other than a local cases directory (e.g. a hosted-journeys
    /// fetch) — there's no cases directory to enumerate, so the fixtures root can't be inferred from
    /// one and must be supplied explicitly. <see cref="LoadAll"/> is not usable on a loader built
    /// this way.
    /// </summary>
    public static CaseFileLoader ForFixturesRoot(string fixturesRoot, Func<string, string?>? resolveEnvironmentVariable = null) =>
        new(fixturesRootOnly: fixturesRoot, resolveEnvironmentVariable);

    private CaseFileLoader(string fixturesRootOnly, Func<string, string?>? resolveEnvironmentVariable)
    {
        _casesDirectory = null;
        _fixturesRoot = fixturesRootOnly;
        _resolveEnvironmentVariable = resolveEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public IReadOnlyList<LoadedCase> LoadAll()
    {
        if (_casesDirectory is null)
        {
            throw new InvalidOperationException($"{nameof(LoadAll)} requires a cases directory; this loader was constructed via {nameof(ForFixturesRoot)}.");
        }

        if (!Directory.Exists(_casesDirectory))
        {
            throw new CaseFileException(_casesDirectory, "cases directory does not exist");
        }

        var files = Directory.EnumerateFiles(_casesDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(_casesDirectory, "*.yml", SearchOption.TopDirectoryOnly))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        return files.Select(f => ParseYaml(Path.GetFileName(f), File.ReadAllText(f))).ToList();
    }

    /// <summary>
    /// Parses one case's YAML content directly, independent of where it came from — the same logic
    /// <see cref="LoadAll"/> uses per file, exposed so a hosted-fetched journey's YAML parses
    /// identically to a locally-loaded case file (only the source of the YAML differs).
    /// </summary>
    public LoadedCase ParseYaml(string label, string yamlContent)
    {
        var fileName = label;

        CaseFileDto? dto;
        try
        {
            dto = _deserializer.Deserialize<CaseFileDto>(yamlContent);
        }
        catch (YamlException ex)
        {
            throw new CaseFileException(fileName, $"invalid YAML: {ex.Message}");
        }

        if (dto is null)
        {
            throw new CaseFileException(fileName, "file is empty");
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new CaseFileException(fileName, "missing required field 'id'");
        }

        if (string.IsNullOrWhiteSpace(dto.Oracle?.Locator))
        {
            throw new CaseFileException(fileName, "missing required field 'oracle.locator'");
        }

        if (string.IsNullOrWhiteSpace(dto.Fixture?.Locator))
        {
            throw new CaseFileException(fileName, "missing required field 'fixture.locator'");
        }

        var fixture = ResolveFixture(fileName, dto.Fixture);

        var prerequisites = (dto.Preconditions ?? new List<PreconditionDto>())
            .Select(p =>
            {
                if (string.IsNullOrWhiteSpace(p.Check))
                {
                    throw new CaseFileException(fileName, "a precondition is missing 'check'");
                }

                if (string.IsNullOrWhiteSpace(p.Owner))
                {
                    throw new CaseFileException(fileName, "a precondition is missing 'owner'");
                }

                return new PrerequisiteDeclaration(p.Check, p.Owner);
            })
            .ToList();

        var pipeline = (dto.Pipeline ?? new List<PipelineStepDto>())
            .Select(p =>
            {
                if (string.IsNullOrWhiteSpace(p.Operation))
                {
                    throw new CaseFileException(fileName, "a pipeline step is missing 'operation'");
                }

                var parameters = ConvertParameters(p.With) is { } converted
                    ? (IReadOnlyDictionary<string, object?>)InterpolateEnvVars(fileName, converted)!
                    : null;

                var captures = (p.Capture ?? new List<CaptureDto>())
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

                return new PipelineStep(p.Operation, With: parameters, Capture: captures.Count > 0 ? captures : null);
            })
            .ToList();

        var cleanup = (dto.Cleanup ?? new List<CleanupDto>())
            .Select(c =>
            {
                if (string.IsNullOrWhiteSpace(c.Operation))
                {
                    throw new CaseFileException(fileName, "a cleanup step is missing 'operation'");
                }

                return new CleanupDeclaration(c.Operation);
            })
            .ToList();

        var requiredCapabilities = (dto.Requires ?? new List<string>())
            .Select(r => new CapabilityRequirement(r))
            .ToList();

        var release = ResolveRelease(fileName, dto.Release);

        var testCase = new TestCase(
            dto.Id,
            new OracleReference(dto.Oracle!.Locator!),
            fixture,
            prerequisites,
            pipeline,
            cleanup,
            string.IsNullOrWhiteSpace(dto.ResourceKey) ? null : new ResourceKey(dto.ResourceKey),
            requiredCapabilities)
        {
            Release = release,
        };

        return new LoadedCase(testCase, ResolveFlagProof(fileName, dto.FlagProof), ResolveEvidenceRules(fileName, dto.Evidence));
    }

    private static string? ResolveRelease(string fileName, object? raw)
    {
        switch (raw)
        {
            case null:
                return null;
            case string s:
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            default:
                // A mapping or sequence — YamlDotNet hands those back as Dictionary/List. The
                // label is a short grouping tag, not structured data.
                throw new CaseFileException(fileName, "field 'release' must be a short string, not a list or mapping");
        }
    }

    private static EvidenceRules ResolveEvidenceRules(string fileName, EvidenceDto? dto)
    {
        if (dto is null)
        {
            return EvidenceRules.None;
        }

        var allow = (dto.Capture ?? new List<string>())
            .Select(c =>
            {
                if (string.IsNullOrWhiteSpace(c))
                {
                    throw new CaseFileException(fileName, "an evidence.capture entry is empty");
                }

                return c.Trim();
            })
            .ToList();

        var redact = (dto.Redact ?? new List<EvidenceRedactDto>())
            .Select(r =>
            {
                var set = new List<(EvidenceRedactKind Kind, string? Value)>
                {
                    (EvidenceRedactKind.Header, r.Header),
                    (EvidenceRedactKind.JsonPath, r.JsonPath),
                    (EvidenceRedactKind.Field, r.Field),
                    (EvidenceRedactKind.Selector, r.Selector),
                    (EvidenceRedactKind.Region, r.Region),
                }
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList();

                if (set.Count != 1)
                {
                    throw new CaseFileException(fileName,
                        "an evidence.redact rule must set exactly one of: header, json_path, field, selector, region");
                }

                return new EvidenceRedactRule(set[0].Kind, set[0].Value!.Trim());
            })
            .ToList();

        return new EvidenceRules(allow, redact);
    }

    private static readonly HashSet<string> AllowedControlMethods =
        new(new[] { "GET", "PUT", "POST", "PATCH", "DELETE" }, StringComparer.OrdinalIgnoreCase);

    private FlagProofDeclaration? ResolveFlagProof(string fileName, FlagProofDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.FeatureKey))
        {
            throw new CaseFileException(fileName, "flag_proof is missing 'feature_key'");
        }

        if (string.IsNullOrWhiteSpace(dto.BuildIdentity))
        {
            throw new CaseFileException(fileName, "flag_proof is missing 'build_identity'");
        }

        return new FlagProofDeclaration(dto.FeatureKey, dto.BuildIdentity, ResolveFlagProofControl(fileName, dto.Control));
    }

    private FlagProofControl? ResolveFlagProofControl(string fileName, FlagProofControlDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Method) || !AllowedControlMethods.Contains(dto.Method))
        {
            throw new CaseFileException(fileName, "flag_proof.control 'method' must be one of GET, PUT, POST, PATCH, DELETE");
        }

        if (string.IsNullOrWhiteSpace(dto.Url))
        {
            throw new CaseFileException(fileName, "flag_proof.control is missing 'url'");
        }

        var polarity = dto.KnownBadWhen?.Trim().ToLowerInvariant() switch
        {
            null or "" or "disabled" => FlagProofPolarity.KnownBadWhenDisabled,
            "enabled" => FlagProofPolarity.KnownBadWhenEnabled,
            _ => throw new CaseFileException(fileName, "flag_proof.control 'known_bad_when' must be 'disabled' or 'enabled'"),
        };

        string Env(string value) => (string)InterpolateEnvVars(fileName, value)!;

        var headers = (dto.Headers ?? new Dictionary<string, string>())
            .ToDictionary(kv => kv.Key, kv => Env(kv.Value));

        return new FlagProofControl(
            dto.Method.ToUpperInvariant(),
            Env(dto.Url),
            headers,
            dto.Body is null ? null : Env(dto.Body),
            polarity,
            ResolveFlagProofControlVerify(fileName, dto.Verify, Env),
            ResolveFlagProofControlAuth(fileName, dto.Auth, Env));
    }

    private static FlagProofControlAuth? ResolveFlagProofControlAuth(
        string fileName, FlagProofControlAuthDto? dto, Func<string, string> env)
    {
        if (dto is null)
        {
            return null;
        }

        var oauth = dto.Oauth2ClientCredentials
            ?? throw new CaseFileException(fileName,
                "flag_proof.control.auth must declare an 'oauth2_client_credentials' block");

        if (string.IsNullOrWhiteSpace(oauth.TokenUrl))
        {
            throw new CaseFileException(fileName, "flag_proof.control.auth.oauth2_client_credentials is missing 'token_url'");
        }

        if (string.IsNullOrWhiteSpace(oauth.ClientId))
        {
            throw new CaseFileException(fileName, "flag_proof.control.auth.oauth2_client_credentials is missing 'client_id'");
        }

        if (string.IsNullOrWhiteSpace(oauth.ClientSecret))
        {
            throw new CaseFileException(fileName, "flag_proof.control.auth.oauth2_client_credentials is missing 'client_secret'");
        }

        return new FlagProofControlAuth(
            env(oauth.TokenUrl),
            env(oauth.ClientId),
            env(oauth.ClientSecret),
            string.IsNullOrWhiteSpace(oauth.Scope) ? null : env(oauth.Scope));
    }

    private static FlagProofControlVerify? ResolveFlagProofControlVerify(
        string fileName, FlagProofControlVerifyDto? dto, Func<string, string> env)
    {
        if (dto is null)
        {
            return null;
        }

        // method defaults to GET; when supplied it must be a known verb.
        var method = string.IsNullOrWhiteSpace(dto.Method) ? "GET" : dto.Method.Trim();
        if (!AllowedControlMethods.Contains(method))
        {
            throw new CaseFileException(fileName,
                "flag_proof.control.verify 'method' must be one of GET, PUT, POST, PATCH, DELETE");
        }

        if (string.IsNullOrWhiteSpace(dto.Url))
        {
            throw new CaseFileException(fileName, "flag_proof.control.verify is missing 'url'");
        }

        if (string.IsNullOrWhiteSpace(dto.JsonPath))
        {
            throw new CaseFileException(fileName, "flag_proof.control.verify is missing 'json_path'");
        }

        if (string.IsNullOrWhiteSpace(dto.Expected))
        {
            throw new CaseFileException(fileName, "flag_proof.control.verify is missing 'expected'");
        }

        var headers = dto.Headers is null
            ? null
            : dto.Headers.ToDictionary(kv => kv.Key, kv => env(kv.Value));

        return new FlagProofControlVerify(
            method.ToUpperInvariant(),
            env(dto.Url),
            headers,
            dto.Body is null ? null : env(dto.Body),
            dto.JsonPath.Trim(),
            env(dto.Expected));
    }

    private static Dictionary<string, object?>? ConvertParameters(object? node) => ConvertYamlNode(node) as Dictionary<string, object?>;

    private static object? ConvertYamlNode(object? node)
    {
        switch (node)
        {
            case Dictionary<object, object> map:
                return map.ToDictionary(kv => kv.Key.ToString()!, kv => ConvertYamlNode(kv.Value));
            case List<object> list:
                return list.Select(ConvertYamlNode).ToList();
            default:
                return node;
        }
    }

    private object? InterpolateEnvVars(string fileName, object? value)
    {
        switch (value)
        {
            case string s:
                return EnvVarPattern.Replace(s, match =>
                {
                    var varName = match.Groups[1].Value;
                    var resolved = _resolveEnvironmentVariable(varName);
                    if (resolved is null)
                    {
                        throw new CaseFileException(fileName, $"a parameter references undefined environment variable '{varName}'");
                    }

                    return resolved;
                });
            case Dictionary<string, object?> dict:
                return dict.ToDictionary(kv => kv.Key, kv => InterpolateEnvVars(fileName, kv.Value));
            case List<object?> list:
                return list.Select(v => InterpolateEnvVars(fileName, v)).ToList();
            default:
                return value;
        }
    }

    private FixtureReference ResolveFixture(string fileName, FixtureDto dto)
    {
        var locator = dto.Locator!;

        if (Path.IsPathRooted(locator) || locator.Replace('\\', '/').Split('/').Any(segment => segment == ".."))
        {
            throw new CaseFileException(fileName, $"fixture locator '{locator}' must be a relative path with no '..' and no absolute path");
        }

        var fixturesRootFull = Path.GetFullPath(_fixturesRoot);
        var candidate = Path.GetFullPath(Path.Combine(fixturesRootFull, locator));
        var prefix = fixturesRootFull.EndsWith(Path.DirectorySeparatorChar)
            ? fixturesRootFull
            : fixturesRootFull + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CaseFileException(fileName, $"fixture locator '{locator}' escapes the fixtures root");
        }

        if (!File.Exists(candidate))
        {
            throw new CaseFileException(fileName, $"fixture file not found: {candidate}");
        }

        var content = File.ReadAllBytes(candidate);
        var actualHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(dto.Sha256) && !string.Equals(actualHash, dto.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new CaseFileException(fileName, $"fixture hash mismatch: declared={dto.Sha256.Trim()} actual={actualHash}");
        }

        return new FixtureReference(locator, string.IsNullOrWhiteSpace(dto.Sha256) ? actualHash : dto.Sha256.Trim(), content);
    }
}
