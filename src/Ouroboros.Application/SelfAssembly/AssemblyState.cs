namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// State of the assembly pipeline for a proposal.
/// </summary>
public record AssemblyState(
    Guid ProposalId,
    AssemblyProposalStatus Status,
    DateTime Timestamp,
    string? Details = null);