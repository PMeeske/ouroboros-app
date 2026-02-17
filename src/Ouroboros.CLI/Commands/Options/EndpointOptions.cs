using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Remote endpoint connection settings.
/// Shared by: ask, pipeline, ouroboros, orchestrator, skills.
/// </summary>
public sealed class EndpointOptions : IComposableOptions
{
    public Option<string?> EndpointOption { get; } = new("--endpoint")
    {
        Description = "Remote endpoint URL"
    };

    public Option<string?> ApiKeyOption { get; } = new("--api-key")
    {
        Description = "API key for remote endpoint"
    };

    public Option<string?> EndpointTypeOption { get; } = new("--endpoint-type")
    {
        Description = "Provider type: auto|anthropic|openai|azure|google|mistral|deepseek|groq|together|fireworks|perplexity|cohere|ollama|github-models|litellm|huggingface|replicate"
    };

    public void AddToCommand(Command command)
    {
        command.Add(EndpointOption);
        command.Add(ApiKeyOption);
        command.Add(EndpointTypeOption);
    }
}