namespace ReleaseTwin.Core;

/// <summary>
/// Closed set fixed by the core (design.md D5) so reports stay comparable across adapters.
/// Adapters map their own error conditions onto these four values rather than inventing new ones.
/// </summary>
public enum FailureClassification
{
    Prerequisite,
    Product,
    Infrastructure,
    Unstable,
}
