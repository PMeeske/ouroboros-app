// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Orchestrates the core chat pipeline: prompt construction, smart tool selection,
/// LLM call, tool result integration, learning recording, and thought persistence.
/// </summary>
public interface IChatSubsystem : IAgentSubsystem
{
    /// <summary>Runs a full chat turn and returns the agent's response.</summary>
    Task<string> ChatAsync(string input);

    /// <summary>Uses LLM to integrate raw tool results into a natural conversational response.</summary>
    Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults);
}
