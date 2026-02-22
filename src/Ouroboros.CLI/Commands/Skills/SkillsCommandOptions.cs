using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the skills command. Composes shared option groups and adds skills-specific options.
/// </summary>
public sealed class SkillsCommandOptions
{
    // ── Composed option groups ─────────────────────────────────────────
    public ModelOptions Model { get; } = new();
    public EndpointOptions Endpoint { get; } = new();
    public MultiModelOptions MultiModel { get; } = new();
    public DiagnosticOptions Diagnostics { get; } = new();
    public EmbeddingOptions Embedding { get; } = new();

    // ── Skills-specific options ────────────────────────────────────────

    public Option<bool> ListOption { get; } = new("--list")
    {
        Description = "List all skills",
        DefaultValueFactory = _ => false
    };

    public Option<string?> FetchOption { get; } = new("--fetch")
    {
        Description = "Fetch research and extract skills"
    };

    public Option<string?> CultureOption { get; } = new("--culture")
    {
        Description = "Target culture for the response"
    };

    public void AddToCommand(Command command)
    {
        // Skills-specific
        command.Add(ListOption);
        command.Add(FetchOption);
        command.Add(CultureOption);

        // Composed groups
        Model.AddToCommand(command);
        Endpoint.AddToCommand(command);
        MultiModel.AddToCommand(command);
        Diagnostics.AddToCommand(command);
        Embedding.AddToCommand(command);
    }
}
