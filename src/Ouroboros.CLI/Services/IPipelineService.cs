namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling pipeline commands
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Executes a pipeline DSL
    /// </summary>
    Task<string> ExecutePipelineAsync(string dsl);
}