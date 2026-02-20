// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.CLI.Commands;

/// <summary>
/// Parses raw user input into typed actions and provides system-level
/// informational responses (help, status, mood, DSL tokens, tool listing).
/// </summary>
public interface ICommandRoutingSubsystem : IAgentSubsystem
{
    /// <summary>Parses raw user input into a typed action triple.</summary>
    (ActionType Type, string Argument, string? ToolInput) ParseAction(string input);

    /// <summary>Returns the full help text for all commands.</summary>
    string GetHelpText();

    /// <summary>Returns a concise status summary of all active subsystems.</summary>
    string GetStatus();

    /// <summary>Returns the agent's current mood as a human-readable string.</summary>
    string GetMood();

    /// <summary>Explains a pipeline DSL expression.</summary>
    string ExplainDsl(string dsl);

    /// <summary>Lists all available DSL tokens.</summary>
    string GetDslTokens();

    /// <summary>Lists all registered tools.</summary>
    string ListTools();

    /// <summary>Routes a slash-command to the autonomous coordinator.</summary>
    string ProcessCoordinatorCommand(string input);
}
