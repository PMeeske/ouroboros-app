
namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling ask commands
/// </summary>
public interface IAskService
{
    /// <summary>
    /// Asks a question to the LLM
    /// </summary>
    Task<string> AskAsync(string question, bool useRag = false);
}