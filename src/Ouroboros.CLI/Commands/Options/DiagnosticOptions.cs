using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Debug and diagnostic output options.
/// Shared by: ask, pipeline, ouroboros, orchestrator, skills.
/// </summary>
public sealed class DiagnosticOptions : IComposableOptions
{
    public Option<bool> DebugOption { get; } = new("--debug")
    {
        Description = "Enable verbose debug logging",
        DefaultValueFactory = _ => false
    };

    public Option<bool> StrictModelOption { get; } = new("--strict-model")
    {
        Description = "Fail instead of falling back when remote model is invalid",
        DefaultValueFactory = _ => false
    };

    public Option<bool> JsonToolsOption { get; } = new("--json-tools")
    {
        Description = "Force JSON tool call format",
        DefaultValueFactory = _ => false
    };

    public void AddToCommand(Command command)
    {
        command.Add(DebugOption);
        command.Add(StrictModelOption);
        command.Add(JsonToolsOption);
    }
}