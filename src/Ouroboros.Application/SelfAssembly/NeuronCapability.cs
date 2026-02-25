namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Capability categories for assembled neurons.
/// </summary>
[Flags]
public enum NeuronCapability
{
    None = 0,
    TextProcessing = 1,
    ApiIntegration = 2,
    Computation = 4,
    FileAccess = 8,          // Forbidden by default
    DataPersistence = 16,
    Reasoning = 32,
    EventObservation = 64,
    Orchestration = 128
}