namespace Ouroboros.Application.Services;

/// <summary>
/// Types of thought atoms.
/// </summary>
public enum ThoughtAtomType
{
    /// <summary>Initial query atom.</summary>
    Query,

    /// <summary>Derived fact atom.</summary>
    Derivation,

    /// <summary>Exploration step atom.</summary>
    Exploration,

    /// <summary>Potential solution atom.</summary>
    Solution,

    /// <summary>Integrated insight from convergence.</summary>
    Insight,

    /// <summary>Self-referential Ouroboros atom.</summary>
    Ouroboros,
}