namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Event raised when a neuron is successfully assembled.
/// </summary>
public record NeuronAssembledEvent(
    Guid ProposalId,
    string NeuronName,
    Type NeuronType,
    DateTime AssembledAt);