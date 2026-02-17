namespace Ouroboros.Application.Tools;

/// <summary>
/// LLM action suggestion.
/// </summary>
public sealed record LLMActionSuggestion(
    string ActionType,
    string ActionName,
    string Reasoning);