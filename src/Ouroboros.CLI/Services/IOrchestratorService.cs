namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling orchestrator commands
/// </summary>
public interface IOrchestratorService
{
    /// <summary>
    /// Orchestrates multiple models to achieve a goal
    /// </summary>
    Task<string> OrchestrateAsync(string goal);
}