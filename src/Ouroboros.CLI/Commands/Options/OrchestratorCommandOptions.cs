using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the orchestrator command using System.CommandLine 2.0.3 GA
/// </summary>
public class OrchestratorCommandOptions
{
    public System.CommandLine.Option<string> GoalOption { get; } = new("--goal")
    {
        Description = "Goal for the orchestrator",
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

    public System.CommandLine.Option<bool> DebugOption { get; } = new("--debug")
    {
        Description = "Enable verbose debug logging",
        DefaultValueFactory = _ => false
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

    public System.CommandLine.Option<string> DecomposeOption { get; } = new("--decompose")
    {
        Description = "Enable goal decomposition mode: off|auto|local-first|quality-first",
        DefaultValueFactory = _ => "off"
    };

    public System.CommandLine.Option<string> CollectiveOption { get; } = new("--collective")
    {
        Description = "Enable CollectiveMind multi-provider mode",
        DefaultValueFactory = _ => "off"
    };

    public System.CommandLine.Option<string> MasterModelOption { get; } = new("--master-model")
    {
        Description = "Designate master model for orchestration"
    };

    public System.CommandLine.Option<string> ElectionStrategyOption { get; } = new("--election-strategy")
    {
        Description = "Election strategy: majority|weighted|borda|condorcet|runoff|approval|master",
        DefaultValueFactory = _ => "weighted"
    };

    public System.CommandLine.Option<bool> ShowSubgoalsOption { get; } = new("--show-subgoals")
    {
        Description = "Display sub-goal decomposition trace",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> ParallelSubgoalsOption { get; } = new("--parallel-subgoals")
    {
        Description = "Execute independent sub-goals in parallel",
        DefaultValueFactory = _ => true
    };

    /// <summary>
    /// Adds all orchestrator command options to the given command.
    /// </summary>
    public void AddToCommand(Command command)
    {
        command.Add(GoalOption);
        command.Add(CultureOption);
        command.Add(ModelOption);
        command.Add(EmbedOption);
        command.Add(TemperatureOption);
        command.Add(MaxTokensOption);
        command.Add(TimeoutSecondsOption);
        command.Add(StreamOption);
        command.Add(RouterOption);
        command.Add(CoderModelOption);
        command.Add(SummarizeModelOption);
        command.Add(ReasonModelOption);
        command.Add(GeneralModelOption);
        command.Add(DebugOption);
        command.Add(StrictModelOption);
        command.Add(JsonToolsOption);
        command.Add(EndpointOption);
        command.Add(ApiKeyOption);
        command.Add(EndpointTypeOption);
        command.Add(DecomposeOption);
        command.Add(CollectiveOption);
        command.Add(MasterModelOption);
        command.Add(ElectionStrategyOption);
        command.Add(ShowSubgoalsOption);
        command.Add(ParallelSubgoalsOption);
    }
}
