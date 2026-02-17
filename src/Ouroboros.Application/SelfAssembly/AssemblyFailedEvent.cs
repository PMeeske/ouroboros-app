namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Event raised when assembly fails.
/// </summary>
public record AssemblyFailedEvent(
    Guid ProposalId,
    string NeuronName,
    string Reason,
    DateTime FailedAt);