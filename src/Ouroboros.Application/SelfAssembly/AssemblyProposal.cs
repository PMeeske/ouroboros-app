namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Assembly proposal requiring approval.
/// </summary>
public record AssemblyProposal(
    Guid Id,
    NeuronBlueprint Blueprint,
    MeTTaValidation Validation,
    string GeneratedCode,
    DateTime ProposedAt,
    AssemblyProposalStatus Status = AssemblyProposalStatus.PendingApproval);