namespace ReleaseTwin.Core;

/// <summary>Reports which capabilities are available in the current composition. Implemented by the adapter SDK's composition root, aggregating declarations from installed adapters.</summary>
public interface ICapabilityCatalog
{
    bool IsAvailable(string capabilityName);
}
