using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Multi-model routing and orchestration options.
/// Shared by: ask, pipeline, orchestrator.
/// </summary>
public sealed class MultiModelOptions : IComposableOptions
{
    public Option<string> RouterOption { get; } = new("--router")
    {
        Description = "Enable multi-model routing: off|auto",
        DefaultValueFactory = _ => "off"
    };

    public Option<string?> CoderModelOption { get; } = new("--coder-model")
    {
        Description = "Model for code/refactor prompts"
    };

    public Option<string?> SummarizeModelOption { get; } = new("--summarize-model")
    {
        Description = "Model for long / summarization prompts"
    };

    public Option<string?> ReasonModelOption { get; } = new("--reason-model")
    {
        Description = "Model for strategic reasoning prompts"
    };

    public Option<string?> GeneralModelOption { get; } = new("--general-model")
    {
        Description = "Fallback general model (overrides --model)"
    };

    public void AddToCommand(Command command)
    {
        command.Add(RouterOption);
        command.Add(CoderModelOption);
        command.Add(SummarizeModelOption);
        command.Add(ReasonModelOption);
        command.Add(GeneralModelOption);
    }
}