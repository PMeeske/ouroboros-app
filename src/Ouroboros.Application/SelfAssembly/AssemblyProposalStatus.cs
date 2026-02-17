namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Status of an assembly proposal.
/// </summary>
public enum AssemblyProposalStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Compiling,
    Testing,
    Deployed,
    Failed
}