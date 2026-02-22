
namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling ask commands.
/// </summary>
public interface IAskService
{
    /// <summary>
    /// Asks a question using the full set of CLI options.
    /// Preferred overload — properly threads all parsed CLI flags through to the LLM.
    /// </summary>
    Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simplified overload for callers that only need question + RAG flag
    /// (e.g. ChatCommand, InteractiveCommand).
    /// Delegates to <see cref="AskAsync(AskRequest, CancellationToken)"/> with defaults.
    /// </summary>
    Task<string> AskAsync(string question, bool useRag = false);
}