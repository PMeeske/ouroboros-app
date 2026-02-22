namespace Ouroboros.CLI.Commands;

/// <summary>
/// Interaction outcome for learning.
/// </summary>
public sealed record InteractionOutcome(
    string UserInput,
    string AgentResponse,
    List<string> ExpectedTools,
    List<string> ActualToolCalls,
    bool WasSuccessful,
    TimeSpan ResponseTime,
    string? UserFeedback = null);