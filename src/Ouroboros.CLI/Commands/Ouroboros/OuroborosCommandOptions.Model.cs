using System.CommandLine;
using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// LLM, Model, and Embeddings options for the ouroboros agent command.
/// </summary>
public partial class OuroborosCommandOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // LLM & MODEL CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<string> ModelOption { get; } = new("--model", "-m")
    {
        Description = "LLM model name",
        DefaultValueFactory = _ => "deepseek-v3.1:671b-cloud"
    };

    public Option<string?> CultureOption { get; } = new("--culture", "-c")
    {
        Description = "Target culture for the response (e.g. en-US, fr-FR, es)"
    };

    public Option<string> EndpointOption { get; } = new("--endpoint")
    {
        Description = "LLM endpoint URL",
        DefaultValueFactory = _ => DefaultEndpoints.Ollama
    };

    public Option<string?> ApiKeyOption { get; } = new("--api-key")
    {
        Description = "API key for remote endpoint"
    };

    public Option<string?> EndpointTypeOption { get; } = new("--endpoint-type")
    {
        Description = "Provider type: auto|anthropic|openai|azure|google|mistral|deepseek|groq|together|fireworks|perplexity|cohere|ollama|github-models|litellm|huggingface|replicate"
    };

    public Option<double> TemperatureOption { get; } = new("--temperature")
    {
        Description = "Sampling temperature",
        DefaultValueFactory = _ => 0.7
    };

    public Option<int> MaxTokensOption { get; } = new("--max-tokens")
    {
        Description = "Max tokens for completion",
        DefaultValueFactory = _ => 2048
    };

    public Option<int> TimeoutSecondsOption { get; } = new("--timeout")
    {
        Description = "Request timeout in seconds",
        DefaultValueFactory = _ => 120
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // EMBEDDINGS & MEMORY
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public Option<string> EmbedEndpointOption { get; } = new("--embed-endpoint")
    {
        Description = "Embedding endpoint (defaults to local Ollama)",
        DefaultValueFactory = _ => DefaultEndpoints.Ollama
    };

    public Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for persistent memory",
        DefaultValueFactory = _ => DefaultEndpoints.QdrantGrpc
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<string?> CoderModelOption { get; } = new("--coder-model")
    {
        Description = "Model for code/refactor tasks"
    };

    public Option<string?> ReasonModelOption { get; } = new("--reason-model")
    {
        Description = "Model for strategic reasoning"
    };

    public Option<string?> SummarizeModelOption { get; } = new("--summarize-model")
    {
        Description = "Model for summarization"
    };

    public Option<string?> VisionModelOption { get; } = new("--vision-model")
    {
        Description = "Model for visual understanding (e.g. qwen3-vl:235b-cloud)"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // AGENT BEHAVIOR
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<int> AgentMaxStepsOption { get; } = new("--agent-max-steps")
    {
        Description = "Max steps for agent planning",
        DefaultValueFactory = _ => 10
    };

    public Option<int> ThinkingIntervalOption { get; } = new("--thinking-interval")
    {
        Description = "Seconds between autonomous thoughts",
        DefaultValueFactory = _ => 30
    };
}
