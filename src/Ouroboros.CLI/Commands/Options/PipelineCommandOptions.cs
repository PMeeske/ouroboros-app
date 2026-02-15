using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the pipeline command using System.CommandLine 2.0.3 GA
/// </summary>
public class PipelineCommandOptions
{
    public System.CommandLine.Option<string> DslOption { get; } = new("--dsl")
    {
        Description = "Pipeline DSL string",
        DefaultValueFactory = _ => string.Empty
    };

    public System.CommandLine.Option<string> CultureOption { get; } = new("--culture")
    {
        Description = "Target culture for the response"
    };

    public System.CommandLine.Option<string> ModelOption { get; } = new("--model")
    {
        Description = "LLM model name",
        DefaultValueFactory = _ => "ministral-3:latest"
    };

    public System.CommandLine.Option<string> EmbedOption { get; } = new("--embed")
    {
        Description = "Ollama embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public System.CommandLine.Option<string> SourceOption { get; } = new("--source")
    {
        Description = "Ingestion/source folder path",
        DefaultValueFactory = _ => "."
    };

    public System.CommandLine.Option<int> TopKOption { get; } = new("--topk")
    {
        Description = "Similarity retrieval k",
        DefaultValueFactory = _ => 8
    };

    public System.CommandLine.Option<bool> TraceOption { get; } = new("--trace")
    {
        Description = "Enable live trace output",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> DebugOption { get; } = new("--debug")
    {
        Description = "Enable verbose debug logging",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<double> TemperatureOption { get; } = new("--temperature")
    {
        Description = "Sampling temperature (remote models)",
        DefaultValueFactory = _ => 0.7
    };

    public System.CommandLine.Option<int> MaxTokensOption { get; } = new("--max-tokens")
    {
        Description = "Max tokens for completion (remote models)",
        DefaultValueFactory = _ => 512
    };

    public System.CommandLine.Option<int> TimeoutSecondsOption { get; } = new("--timeout-seconds")
    {
        Description = "HTTP timeout for remote model",
        DefaultValueFactory = _ => 60
    };

    public System.CommandLine.Option<bool> StreamOption { get; } = new("--stream")
    {
        Description = "Stream output (simulated for now)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> RouterOption { get; } = new("--router")
    {
        Description = "Enable multi-model routing: off|auto",
        DefaultValueFactory = _ => "off"
    };

    public System.CommandLine.Option<string> CoderModelOption { get; } = new("--coder-model")
    {
        Description = "Model for code/refactor prompts"
    };

    public System.CommandLine.Option<string> SummarizeModelOption { get; } = new("--summarize-model")
    {
        Description = "Model for long / summarization prompts"
    };

    public System.CommandLine.Option<string> ReasonModelOption { get; } = new("--reason-model")
    {
        Description = "Model for strategic reasoning prompts"
    };

    public System.CommandLine.Option<string> GeneralModelOption { get; } = new("--general-model")
    {
        Description = "Fallback general model (overrides --model)"
    };

    public System.CommandLine.Option<bool> AgentOption { get; } = new("--agent")
    {
        Description = "Enable iterative agent loop with tool execution",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> AgentModeOption { get; } = new("--agent-mode")
    {
        Description = "Agent implementation: simple|lc|react|self-critique",
        DefaultValueFactory = _ => "lc"
    };

    public System.CommandLine.Option<int> AgentMaxStepsOption { get; } = new("--agent-max-steps")
    {
        Description = "Max iterations for agent loop",
        DefaultValueFactory = _ => 6
    };

    public System.CommandLine.Option<int> CritiqueIterationsOption { get; } = new("--critique-iterations")
    {
        Description = "Number of critique-improve cycles for self-critique mode",
        DefaultValueFactory = _ => 1
    };

    public System.CommandLine.Option<bool> StrictModelOption { get; } = new("--strict-model")
    {
        Description = "Fail instead of falling back when remote model invalid",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> JsonToolsOption { get; } = new("--json-tools")
    {
        Description = "Force JSON tool call format",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> EndpointOption { get; } = new("--endpoint")
    {
        Description = "Remote endpoint URL (overrides CHAT_ENDPOINT env var)"
    };

    public System.CommandLine.Option<string> ApiKeyOption { get; } = new("--api-key")
    {
        Description = "API key for remote endpoint (overrides CHAT_API_KEY env var)"
    };

    public System.CommandLine.Option<string> EndpointTypeOption { get; } = new("--endpoint-type")
    {
        Description = "Endpoint type: auto|openai|ollama-cloud|litellm|github-models"
    };

    /// <summary>
    /// Adds all pipeline command options to the given command.
    /// </summary>
    public void AddToCommand(Command command)
    {
        command.Add(DslOption);
        command.Add(CultureOption);
        command.Add(ModelOption);
        command.Add(EmbedOption);
        command.Add(SourceOption);
        command.Add(TopKOption);
        command.Add(TraceOption);
        command.Add(DebugOption);
        command.Add(TemperatureOption);
        command.Add(MaxTokensOption);
        command.Add(TimeoutSecondsOption);
        command.Add(StreamOption);
        command.Add(RouterOption);
        command.Add(CoderModelOption);
        command.Add(SummarizeModelOption);
        command.Add(ReasonModelOption);
        command.Add(GeneralModelOption);
        command.Add(AgentOption);
        command.Add(AgentModeOption);
        command.Add(AgentMaxStepsOption);
        command.Add(CritiqueIterationsOption);
        command.Add(StrictModelOption);
        command.Add(JsonToolsOption);
        command.Add(EndpointOption);
        command.Add(ApiKeyOption);
        command.Add(EndpointTypeOption);
    }
}
