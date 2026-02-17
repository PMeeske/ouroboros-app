namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Configuration for the self-assembly engine.
/// </summary>
public record SelfAssemblyConfig
{
    public bool AutoApprovalEnabled { get; init; } = false;
    public double AutoApprovalThreshold { get; init; } = 0.95;
    public double MinSafetyScore { get; init; } = 0.8;
    public int MaxAssembledNeurons { get; init; } = 10;
    public HashSet<NeuronCapability> ForbiddenCapabilities { get; init; } = new() { NeuronCapability.FileAccess };
    public TimeSpan SandboxTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Additional namespaces to forbid beyond the default set.
    /// Default forbidden namespaces include System.Net, System.IO, System.Diagnostics.Process, etc.
    /// </summary>
    public IReadOnlyList<string> AdditionalForbiddenNamespaces { get; init; } = [];
}