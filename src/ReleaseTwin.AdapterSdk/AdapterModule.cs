using ReleaseTwin.Core;

namespace ReleaseTwin.AdapterSdk;

/// <summary>
/// The contract an adapter implements to contribute operations, prerequisite checks, cleanup handlers,
/// and capability declarations to a composition, without any core type needing to know the adapter exists.
/// </summary>
public interface IAdapterModule
{
    string Name { get; }

    void Register(IAdapterRegistrationBuilder builder);
}

public interface IAdapterRegistrationBuilder
{
    IAdapterRegistrationBuilder AddOperation(string name, IOperation operation);
    IAdapterRegistrationBuilder AddPrerequisite(string name, IPrerequisiteCheck check);
    IAdapterRegistrationBuilder AddCleanup(string name, ICleanupOperation operation);
    IAdapterRegistrationBuilder AddCapability(string name);
}

/// <summary>
/// Optional marker an <see cref="IAdapterModule"/> implements when it can vend a feature-state
/// controller for flag-proof mode — lets a caller ask "whichever installed adapter exposes one"
/// instead of depending on any specific adapter by name.
/// </summary>
public interface IFeatureStateControllerSource
{
    IFeatureStateController? FeatureStateController { get; }
}
