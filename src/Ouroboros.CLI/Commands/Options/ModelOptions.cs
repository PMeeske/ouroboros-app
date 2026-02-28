using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// LLM model selection and inference configuration.
/// Shared by: ask, pipeline, ouroboros, orchestrator, skills.
/// </summary>
public sealed class ModelOptions : IComposableOptions
{
    public Option<string> ModelOption { get; } = new("--model", "-m")
    {
        Description = "LLM model name",
        DefaultValueFactory = _ => "deepseek-v3.1:671b-cloud"
    };

    public Option<double> TemperatureOption { get; } = new("--temperature")
    {
        Description = "Sampling temperature",
        DefaultValueFactory = _ => 0.7
    };

    public Option<int> MaxTokensOption { get; } = new("--max-tokens")
    {
        Description = "Max tokens for completion",
        DefaultValueFactory = _ => 0
    };

    public Option<int> TimeoutSecondsOption { get; } = new("--timeout")
    {
        Description = "Request timeout in seconds",
        DefaultValueFactory = _ => 60
    };

    public Option<bool> StreamOption { get; } = new("--stream")
    {
        Description = "Stream responses as generated",
        DefaultValueFactory = _ => false
    };

    public void AddToCommand(Command command)
    {
        command.Add(ModelOption);
        command.Add(TemperatureOption);
        command.Add(MaxTokensOption);
        command.Add(TimeoutSecondsOption);
        command.Add(StreamOption);
    }
}