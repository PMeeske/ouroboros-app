using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Composable option groups that eliminate duplication across command option classes.
/// Each group represents a coherent set of options that appear on multiple commands.
/// Commands compose the groups they need via <see cref="IComposableOptions"/>.
/// </summary>
public interface IComposableOptions
{
    void AddToCommand(Command command);
}

/// <summary>
/// LLM model selection and inference configuration.
/// Shared by: ask, pipeline, ouroboros, orchestrator, skills.
/// </summary>
public sealed class ModelOptions : IComposableOptions
{
    public Option<string> ModelOption { get; } = new("--model", "-m")
    {
        Description = "LLM model name",
        DefaultValueFactory = _ => "ministral-3:latest"
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

/// <summary>
/// Embedding model and vector store configuration.
/// Shared by: ask, pipeline, ouroboros, skills.
/// </summary>
public sealed class EmbeddingOptions : IComposableOptions
{
    public Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for persistent memory",
        DefaultValueFactory = _ => "http://localhost:6334"
    };

    public void AddToCommand(Command command)
    {
        command.Add(EmbedModelOption);
        command.Add(QdrantEndpointOption);
    }
}

/// <summary>
/// Collective mind / multi-provider orchestration.
/// Shared by: ask, orchestrator.
/// </summary>
public sealed class CollectiveOptions : IComposableOptions
{
    public Option<string> CollectiveOption { get; } = new("--collective")
    {
        Description = "Enable CollectiveMind multi-provider mode",
        DefaultValueFactory = _ => "off"
    };

    public Option<string?> MasterModelOption { get; } = new("--master-model")
    {
        Description = "Designate master model for orchestration"
    };

    public Option<string> ElectionStrategyOption { get; } = new("--election-strategy")
    {
        Description = "Election strategy: majority|weighted|borda|condorcet|runoff|approval|master",
        DefaultValueFactory = _ => "weighted"
    };

    public Option<bool> ShowSubgoalsOption { get; } = new("--show-subgoals")
    {
        Description = "Display sub-goal decomposition trace",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ParallelSubgoalsOption { get; } = new("--parallel-subgoals")
    {
        Description = "Execute independent sub-goals in parallel",
        DefaultValueFactory = _ => true
    };

    public Option<string> DecomposeOption { get; } = new("--decompose")
    {
        Description = "Enable goal decomposition mode: off|auto|local-first|quality-first",
        DefaultValueFactory = _ => "off"
    };

    public void AddToCommand(Command command)
    {
        command.Add(CollectiveOption);
        command.Add(MasterModelOption);
        command.Add(ElectionStrategyOption);
        command.Add(ShowSubgoalsOption);
        command.Add(ParallelSubgoalsOption);
        command.Add(DecomposeOption);
    }
}

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

/// <summary>
/// Voice interaction options (for commands that support voice alongside
/// the global --voice flag).
/// </summary>
public sealed class CommandVoiceOptions : IComposableOptions
{
    public Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text output)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Prefer local TTS (Windows SAPI) over cloud",
        DefaultValueFactory = _ => true
    };

    public Option<bool> VoiceLoopOption { get; } = new("--voice-loop")
    {
        Description = "Continue voice conversation after command",
        DefaultValueFactory = _ => false
    };

    public void AddToCommand(Command command)
    {
        command.Add(VoiceOnlyOption);
        command.Add(LocalTtsOption);
        command.Add(VoiceLoopOption);
    }
}
