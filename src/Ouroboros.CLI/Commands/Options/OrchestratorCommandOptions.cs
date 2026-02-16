using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the orchestrator command. Composes shared option groups and adds orchestrator-specific options.
/// </summary>
public sealed class OrchestratorCommandOptions
{
    // ── Composed option groups ─────────────────────────────────────────
    public ModelOptions Model { get; } = new();
    public EndpointOptions Endpoint { get; } = new();
    public MultiModelOptions MultiModel { get; } = new();
    public DiagnosticOptions Diagnostics { get; } = new();
    public CollectiveOptions Collective { get; } = new();

    // ── Orchestrator-specific options ──────────────────────────────────

    public Option<string> GoalOption { get; } = new("--goal", "-g")
    {
        Description = "Goal for the orchestrator",
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

    public void AddToCommand(Command command)
    {
        // Orchestrator-specific
        command.Add(GoalOption);
        command.Add(CultureOption);
        command.Add(EmbedOption);

        // Composed groups
        Model.AddToCommand(command);
        Endpoint.AddToCommand(command);
        MultiModel.AddToCommand(command);
        Diagnostics.AddToCommand(command);
        Collective.AddToCommand(command);
    }
}
