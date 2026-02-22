using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the pipeline command. Composes shared option groups and adds pipeline-specific options.
/// </summary>
public sealed class PipelineCommandOptions
{
    // ── Composed option groups ─────────────────────────────────────────
    public ModelOptions Model { get; } = new();
    public EndpointOptions Endpoint { get; } = new();
    public MultiModelOptions MultiModel { get; } = new();
    public DiagnosticOptions Diagnostics { get; } = new();
    public AgentLoopOptions AgentLoop { get; } = new();

    // ── Pipeline-specific options ──────────────────────────────────────

    public Option<string> DslOption { get; } = new("--dsl")
    {
        Description = "Pipeline DSL string",
        DefaultValueFactory = _ => string.Empty
    };

    public Option<string?> CultureOption { get; } = new("--culture")
    {
        Description = "Target culture for the response"
    };

    public Option<string> EmbedOption { get; } = new("--embed")
    {
        Description = "Ollama embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public Option<string> SourceOption { get; } = new("--source")
    {
        Description = "Ingestion/source folder path",
        DefaultValueFactory = _ => "."
    };

    public Option<int> TopKOption { get; } = new("--topk")
    {
        Description = "Similarity retrieval k",
        DefaultValueFactory = _ => 8
    };

    public Option<bool> TraceOption { get; } = new("--trace")
    {
        Description = "Enable live trace output",
        DefaultValueFactory = _ => false
    };

    public Option<int> CritiqueIterationsOption { get; } = new("--critique-iterations")
    {
        Description = "Number of critique-improve cycles for self-critique mode",
        DefaultValueFactory = _ => 1
    };

    public void AddToCommand(Command command)
    {
        // Pipeline-specific
        command.Add(DslOption);
        command.Add(CultureOption);
        command.Add(EmbedOption);
        command.Add(SourceOption);
        command.Add(TopKOption);
        command.Add(TraceOption);
        command.Add(CritiqueIterationsOption);

        // Composed groups
        Model.AddToCommand(command);
        Endpoint.AddToCommand(command);
        MultiModel.AddToCommand(command);
        Diagnostics.AddToCommand(command);
        AgentLoop.AddToCommand(command);
    }
}
