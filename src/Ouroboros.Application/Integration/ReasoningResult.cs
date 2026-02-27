using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Reasoning;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Result of reasoning operations.
/// </summary>
public sealed record ReasoningResult(
    string Answer,
    Form Certainty,
    IReadOnlyList<Fact> SupportingFacts,
    ProofTrace? Proof,
    CausalGraph? RelevantCauses);