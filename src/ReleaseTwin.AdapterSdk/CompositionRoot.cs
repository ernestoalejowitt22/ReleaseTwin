using ReleaseTwin.Core;

namespace ReleaseTwin.AdapterSdk;

/// <summary>
/// Registers core services first, then installs zero or more adapters. Adapters contribute to a single
/// combined catalog; the core never knows how many adapters exist or what they are named.
/// </summary>
public sealed class CompositionRoot
{
    private readonly RegistrationBuilder _builder = new();

    public CompositionRoot Install(IAdapterModule adapter)
    {
        adapter.Register(_builder);
        return this;
    }

    /// <summary>The combined catalog aggregating every installed adapter's contributions.</summary>
    public CombinedCatalog Catalog => _builder.Build();

    public CaseExecutor BuildExecutor(IResourceSerializer? resourceSerializer = null)
    {
        var catalog = Catalog;
        return new CaseExecutor(catalog, catalog, catalog, catalog, resourceSerializer);
    }

    private sealed class RegistrationBuilder : IAdapterRegistrationBuilder
    {
        private readonly Dictionary<string, IOperation> _operations = new();
        private readonly Dictionary<string, IPrerequisiteCheck> _prerequisites = new();
        private readonly Dictionary<string, ICleanupOperation> _cleanups = new();
        private readonly HashSet<string> _capabilities = new();

        public IAdapterRegistrationBuilder AddOperation(string name, IOperation operation)
        {
            _operations.Add(name, operation);
            return this;
        }

        public IAdapterRegistrationBuilder AddPrerequisite(string name, IPrerequisiteCheck check)
        {
            _prerequisites.Add(name, check);
            return this;
        }

        public IAdapterRegistrationBuilder AddCleanup(string name, ICleanupOperation operation)
        {
            _cleanups.Add(name, operation);
            return this;
        }

        public IAdapterRegistrationBuilder AddCapability(string name)
        {
            _capabilities.Add(name);
            return this;
        }

        public CombinedCatalog Build() =>
            new(new Dictionary<string, IOperation>(_operations),
                new Dictionary<string, IPrerequisiteCheck>(_prerequisites),
                new Dictionary<string, ICleanupOperation>(_cleanups),
                new HashSet<string>(_capabilities));
    }
}

public sealed class CombinedCatalog : IOperationCatalog, IPrerequisiteCatalog, ICleanupCatalog, ICapabilityCatalog
{
    private readonly IReadOnlyDictionary<string, IOperation> _operations;
    private readonly IReadOnlyDictionary<string, IPrerequisiteCheck> _prerequisites;
    private readonly IReadOnlyDictionary<string, ICleanupOperation> _cleanups;
    private readonly IReadOnlySet<string> _capabilities;

    internal CombinedCatalog(
        IReadOnlyDictionary<string, IOperation> operations,
        IReadOnlyDictionary<string, IPrerequisiteCheck> prerequisites,
        IReadOnlyDictionary<string, ICleanupOperation> cleanups,
        IReadOnlySet<string> capabilities)
    {
        _operations = operations;
        _prerequisites = prerequisites;
        _cleanups = cleanups;
        _capabilities = capabilities;
    }

    public bool TryGet(string name, out IOperation operation) => _operations.TryGetValue(name, out operation!);
    public bool TryGet(string name, out IPrerequisiteCheck check) => _prerequisites.TryGetValue(name, out check!);
    public bool TryGet(string name, out ICleanupOperation operation) => _cleanups.TryGetValue(name, out operation!);
    public bool IsAvailable(string capabilityName) => _capabilities.Contains(capabilityName);
}
