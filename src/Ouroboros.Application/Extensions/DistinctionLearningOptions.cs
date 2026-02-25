using Ouroboros.Core.DistinctionLearning;

namespace Ouroboros.Application.Extensions;

/// <summary>
/// Options for configuring distinction learning.
/// </summary>
public sealed record DistinctionLearningOptions
{
    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    public DistinctionStorageConfig? StorageConfig { get; init; }

    /// <summary>
    /// Gets a value indicating whether to enable pipeline integration.
    /// </summary>
    public bool EnablePipelineIntegration { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to enable background consolidation.
    /// </summary>
    public bool EnableBackgroundConsolidation { get; init; } = true;

    /// <summary>
    /// Gets the consolidation interval.
    /// </summary>
    public TimeSpan ConsolidationInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static DistinctionLearningOptions Default => new();
}