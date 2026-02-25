using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Agent execution loop options.
/// Shared by: ask, pipeline.
/// </summary>
public sealed class AgentLoopOptions : IComposableOptions
{
    public Option<bool> AgentOption { get; } = new("--agent")
    {
        Description = "Enable iterative agent loop with tool execution",
        DefaultValueFactory = _ => false
    };

    public Option<string> AgentModeOption { get; } = new("--agent-mode")
    {
        Description = "Agent implementation: simple|lc|react|self-critique",
        DefaultValueFactory = _ => "lc"
    };

    public Option<int> AgentMaxStepsOption { get; } = new("--agent-max-steps")
    {
        Description = "Max iterations for agent loop",
        DefaultValueFactory = _ => 6
    };

    public void AddToCommand(Command command)
    {
        command.Add(AgentOption);
        command.Add(AgentModeOption);
        command.Add(AgentMaxStepsOption);
    }
}